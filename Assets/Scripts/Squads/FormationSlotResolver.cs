using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// NavMesh questions the slot resolver asks. <see cref="FormationSlotResolveSystem"/> answers them with
/// <c>NavMesh.SamplePosition</c> and <c>NavMesh.Raycast</c>; EditMode tests answer them with a fake grid.
/// </summary>
public interface IFormationNavProbe
{
    /// <summary>Nearest point on the NavMesh within <paramref name="radius"/> of <paramref name="point"/>.</summary>
    bool TrySnap(float3 point, float radius, out float3 snapped);

    /// <summary>
    /// Walks the NavMesh in a straight line from <paramref name="from"/> towards <paramref name="to"/>. True when an edge
    /// blocks the way; <paramref name="hit"/> is then where the walk stops.
    /// </summary>
    bool Raycast(float3 from, float3 to, out float3 hit);
}

/// <summary>How a formation slot was resolved against the NavMesh (C13).</summary>
public enum FormationSlotResolveStatus : byte
{
    /// <summary>Not validated (resolver off or not run yet): navigation projects the template slot itself.</summary>
    Pending,
    /// <summary>The template slot is on the NavMesh, in its own cell and reachable from the formation origin.</summary>
    Template,
    /// <summary>The template slot was blocked or taken; the slot moved by whole cells to the nearest free one.</summary>
    Relocated,
    /// <summary>No free reachable cell within the search: navigation falls back to projecting the template slot.</summary>
    Unresolved
}

/// <summary>One slot to resolve: its template world position and its unrotated offset from the formation centre.</summary>
public struct FormationSlotRequest
{
    public float3 template;
    /// <summary>Offset from the formation centre in metres (x = right, y = forward), before rotation.</summary>
    public float2 localOffset;
    /// <summary>Cells the slot was moved at the previous validation; tried first so slots do not swap cells.</summary>
    public int2 previousShift;
    /// <summary>Where the soldier is now: among equally near free cells, the closest to it wins.</summary>
    public float3 unitPosition;
}

/// <summary>Where a slot ends up.</summary>
public struct FormationSlotResolution
{
    public float3 position;
    /// <summary>Whole cells moved from the template, in the formation frame (x = right, y = forward).</summary>
    public int2 shift;
    public FormationSlotResolveStatus status;
}

/// <summary>
/// Validates the slots of one formation against the NavMesh (C13, L3-01; Movimiento_Formaciones.md L-2 and D9).
/// The formation is a local grid whose cell is the spacing. A slot keeps its template position when that position
/// projects onto the NavMesh within a quarter cell and a straight walk from the formation origin reaches it (the "same
/// island" of the reference, checked locally: a slot behind a wall counts as blocked). Any other slot moves by
/// whole cells to the nearest free cell that passes the same test, searching rings of four-neighbour distance around
/// its own cell (a bounded BFS). The template is never rejected: a slot with no free cell stays unresolved and
/// navigation falls back to the template. Pure and deterministic; the caches are reused between calls.
/// </summary>
public sealed class FormationSlotResolver
{
    public struct Settings
    {
        /// <summary>Cell size: the formation spacing, m.</summary>
        public float spacing;
        /// <summary>Largest projection radius for a cell, m (capped at the spacing).</summary>
        public float snapRadius;
        /// <summary>Largest four-neighbour distance searched around a blocked slot, in cells.</summary>
        public int searchCells;
    }

    /// <summary>
    /// Two slots closer than this fraction of a cell (in the formation frame, before projection) share a cell. Slots
    /// are compared by distance rather than by cell index: templates with an even width sit on half cells, and
    /// rounding a half cell with noise would put two neighbours in one index.
    /// </summary>
    public const float SharedCellFraction = 0.5f;

    struct Cell
    {
        public bool valid;
        public float3 position;
    }

    readonly Dictionary<int2, Cell> _cells = new();
    readonly List<float2> _taken = new();
    readonly List<int> _blocked = new();
    static readonly List<int2> RingOffsets = new();
    static int _ringOffsetsRadius = -1;

    /// <summary>
    /// Origin of the reachability test: the formation centre as reached on the NavMesh. With a leader (the hero while
    /// following), the straight walk from the leader towards the centre, stopped at the first edge, so a centre inside a
    /// wall does not jump to the wall's far side. Without one, the centre projected within <paramref name="radius"/>.
    /// </summary>
    public static bool TryOrigin<TProbe>(ref TProbe probe, float3 center, bool hasLeader, float3 leader, float radius,
        out float3 origin) where TProbe : IFormationNavProbe
    {
        if (hasLeader && probe.TrySnap(leader, radius, out float3 leaderOnMesh)
                      && probe.Raycast(leaderOnMesh, center, out origin))
            return true;
        return probe.TrySnap(center, radius, out origin);
    }

    /// <summary>Resolves every slot of one formation. <paramref name="results"/> gets one entry per request, in order.</summary>
    public void Resolve<TProbe>(ref TProbe probe, float3 origin, quaternion rotation, in Settings settings,
        IReadOnlyList<FormationSlotRequest> requests, List<FormationSlotResolution> results)
        where TProbe : IFormationNavProbe
    {
        _cells.Clear();
        _taken.Clear();
        _blocked.Clear();
        results.Clear();
        float spacing = math.max(FormationDefinition.MinSpacing, settings.spacing);
        float snapRadius = math.min(math.max(0.01f, settings.snapRadius), spacing);
        quaternion frame = math.lengthsq(rotation.value) > 1e-6f ? math.normalize(rotation) : quaternion.identity;
        quaternion inverse = math.inverse(frame);

        // Template slots that pass keep their place and take their cell first.
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            if (TestPoint(ref probe, origin, request.template, inverse, spacing, snapRadius, out float3 snapped))
            {
                _taken.Add(request.localOffset);
                results.Add(new FormationSlotResolution { position = snapped, status = FormationSlotResolveStatus.Template });
            }
            else
            {
                _blocked.Add(i);
                results.Add(new FormationSlotResolution
                    { position = request.template, status = FormationSlotResolveStatus.Unresolved });
            }
        }

        // Blocked slots in slot order: their previous cell if still free and good, else the nearest free good cell.
        int searchCells = math.max(1, settings.searchCells);
        EnsureRingOffsets(searchCells);
        foreach (int i in _blocked)
        {
            var request = requests[i];
            float3 position = default;
            bool found = !request.previousShift.Equals(int2.zero)
                         && TryCell(ref probe, origin, request, request.previousShift, frame, inverse, spacing,
                             snapRadius, out position);
            int2 shift = request.previousShift;
            // Ring by ring; within the nearest ring that has a free good cell, the one closest to the soldier, so
            // relocated soldiers do not overtake or cross each other.
            float bestSq = float.MaxValue;
            for (int k = 0; !found && k < RingOffsets.Count; k++)
            {
                if (TryCell(ref probe, origin, request, RingOffsets[k], frame, inverse, spacing, snapRadius,
                        out float3 candidate)
                    && math.distancesq(candidate.xz, request.unitPosition.xz) < bestSq)
                {
                    bestSq = math.distancesq(candidate.xz, request.unitPosition.xz);
                    shift = RingOffsets[k];
                    position = candidate;
                }
                bool ringEnds = k + 1 == RingOffsets.Count || Ring(RingOffsets[k + 1]) != Ring(RingOffsets[k]);
                found = ringEnds && bestSq < float.MaxValue;
            }
            if (!found) continue;
            _taken.Add(request.localOffset + (float2)shift * spacing);
            results[i] = new FormationSlotResolution
                { position = position, shift = shift, status = FormationSlotResolveStatus.Relocated };
        }
    }

    bool TryCell<TProbe>(ref TProbe probe, float3 origin, in FormationSlotRequest request, int2 shift,
        quaternion frame, quaternion inverse, float spacing, float snapRadius, out float3 position)
        where TProbe : IFormationNavProbe
    {
        position = default;
        float2 local = request.localOffset + (float2)shift * spacing;
        float sharedSq = math.square(SharedCellFraction * spacing);
        foreach (float2 taken in _taken)
            if (math.distancesq(taken, local) < sharedSq) return false;
        // Cells are cached by local position on a half-cell lattice, so slots of one template share the answer.
        int2 key = (int2)math.round(local / (SharedCellFraction * spacing));
        if (!_cells.TryGetValue(key, out var state))
        {
            float3 candidate = request.template + math.mul(frame, new float3(shift.x, 0f, shift.y) * spacing);
            state.valid = TestPoint(ref probe, origin, candidate, inverse, spacing, snapRadius, out state.position);
            _cells[key] = state;
        }
        position = state.position;
        return state.valid;
    }

    /// <summary>
    /// A projected slot may slide at most this fraction of a cell along each formation axis: two neighbours that both
    /// slide towards each other still keep half a cell between them, so they never share a cell.
    /// </summary>
    public const float MaxSlideFraction = 0.25f;

    /// <summary>On the NavMesh close to the point (within its cell), and reachable in a straight walk from the origin.</summary>
    static bool TestPoint<TProbe>(ref TProbe probe, float3 origin, float3 point, quaternion inverse, float spacing,
        float snapRadius, out float3 snapped) where TProbe : IFormationNavProbe
    {
        if (!probe.TrySnap(point, snapRadius, out snapped)) return false;
        float3 local = math.mul(inverse, snapped - point);
        float slide = MaxSlideFraction * spacing;
        if (math.abs(local.x) > slide || math.abs(local.z) > slide) return false;
        return !probe.Raycast(origin, snapped, out _);
    }

    /// <summary>
    /// Offsets by ring of four-neighbour distance (what a BFS from the cell visits layer by layer). The order inside a
    /// ring only breaks exact ties of distance to the soldier: nearest first, then the same column, then forward.
    /// </summary>
    static int Ring(int2 offset) => math.abs(offset.x) + math.abs(offset.y);

    static void EnsureRingOffsets(int radius)
    {
        if (_ringOffsetsRadius == radius) return;
        RingOffsets.Clear();
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        {
            int ring = math.abs(x) + math.abs(y);
            if (ring > 0 && ring <= radius) RingOffsets.Add(new int2(x, y));
        }
        RingOffsets.Sort((a, b) =>
        {
            int ra = math.abs(a.x) + math.abs(a.y), rb = math.abs(b.x) + math.abs(b.y);
            if (ra != rb) return ra.CompareTo(rb);
            int da = a.x * a.x + a.y * a.y, db = b.x * b.x + b.y * b.y;
            if (da != db) return da.CompareTo(db);
            if (math.abs(a.x) != math.abs(b.x)) return math.abs(a.x).CompareTo(math.abs(b.x));
            if (a.y != b.y) return b.y.CompareTo(a.y);
            return a.x.CompareTo(b.x);
        });
        _ringOffsetsRadius = radius;
    }
}
