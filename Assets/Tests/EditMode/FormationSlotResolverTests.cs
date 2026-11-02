using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

/// <summary>
/// C13 / L3-01: slots validated against the NavMesh per squad. The NavMesh is a union of walkable rectangles on the
/// ground plane; a straight walk is blocked where it leaves them.
/// </summary>
public class FormationSlotResolverTests
{
    const float Spacing = 1f;
    const float SnapRadius = 0.65f;
    const int SearchCells = 6;

    struct RectProbe : IFormationNavProbe
    {
        public List<float4> rects; // (minX, minZ, maxX, maxZ)
        public int queries;

        bool Inside(float2 p)
        {
            foreach (var r in rects)
                if (p.x >= r.x && p.x <= r.z && p.y >= r.y && p.y <= r.w) return true;
            return false;
        }

        public bool TrySnap(float3 point, float radius, out float3 snapped)
        {
            queries++;
            float best = float.MaxValue;
            snapped = default;
            foreach (var r in rects)
            {
                float2 p = math.clamp(point.xz, r.xy, r.zw);
                float d = math.distance(p, point.xz);
                if (d < best) { best = d; snapped = new float3(p.x, 0f, p.y); }
            }
            return best <= radius;
        }

        public bool Raycast(float3 from, float3 to, out float3 hit)
        {
            queries++;
            const float step = 0.02f;
            float length = math.distance(from.xz, to.xz);
            float3 last = from;
            for (float t = 0f; t <= length; t += step)
            {
                float3 p = math.lerp(from, to, length > 0f ? t / length : 0f);
                if (!Inside(p.xz)) { hit = last; return true; }
                last = p;
            }
            hit = to;
            return !Inside(to.xz);
        }
    }

    static RectProbe Probe(params float4[] rects) => new RectProbe { rects = new List<float4>(rects) };

    /// <summary>A line of <paramref name="width"/> by <paramref name="depth"/> around the origin, facing +z (rotated by <paramref name="rotation"/>).</summary>
    static List<FormationSlotRequest> Line(int width, int depth, quaternion rotation, float3 center = default)
    {
        var requests = new List<FormationSlotRequest>();
        for (int row = 0; row < depth; row++)
        for (int column = 0; column < width; column++)
        {
            var offset = new float2((column - (width - 1) * 0.5f) * Spacing, -row * Spacing);
            requests.Add(new FormationSlotRequest
            {
                template = center + math.mul(rotation, new float3(offset.x, 0f, offset.y)),
                localOffset = offset,
            });
        }
        return requests;
    }

    static FormationSlotResolver.Settings Settings => new FormationSlotResolver.Settings
        { spacing = Spacing, snapRadius = SnapRadius, searchCells = SearchCells };

    static List<FormationSlotResolution> Resolve(ref RectProbe probe, float3 origin, quaternion rotation,
        List<FormationSlotRequest> requests, FormationSlotResolver resolver = null)
    {
        var results = new List<FormationSlotResolution>();
        (resolver ?? new FormationSlotResolver()).Resolve(ref probe, origin, rotation, Settings, requests, results);
        return results;
    }

    static readonly float4 OpenGround = new(-50f, -50f, 50f, 50f);

    /// <summary>Resolves around the origin with other squads' cells taken (F17).</summary>
    static List<FormationSlotResolution> ResolveAround(ref RectProbe probe, List<FormationSlotRequest> requests, List<float2> occupied)
    {
        var settings = Settings;
        settings.occupiedRadius = 0.5f * Spacing;
        var results = new List<FormationSlotResolution>();
        new FormationSlotResolver().Resolve(ref probe, float3.zero, quaternion.identity, settings, requests, results, occupied);
        return results;
    }

    static void AssertNoSharedCells(List<FormationSlotResolution> results)
    {
        for (int i = 0; i < results.Count; i++)
        for (int j = i + 1; j < results.Count; j++)
            Assert.That(math.distance(results[i].position.xz, results[j].position.xz), Is.GreaterThanOrEqualTo(0.5f * Spacing),
                $"Slots {i} and {j} share a cell.");
    }

    [Test]
    public void OpenGroundKeepsEveryTemplateSlotWithTwoQueriesEach()
    {
        var probe = Probe(new float4(-50f, -50f, 50f, 50f));
        var requests = Line(6, 2, quaternion.identity);

        var results = Resolve(ref probe, float3.zero, quaternion.identity, requests);

        Assert.That(probe.queries, Is.EqualTo(2 * requests.Count));
        for (int i = 0; i < requests.Count; i++)
        {
            Assert.That(results[i].status, Is.EqualTo(FormationSlotResolveStatus.Template));
            Assert.That(results[i].shift, Is.EqualTo(int2.zero));
            Assert.That(math.distance(results[i].position, requests[i].template), Is.LessThan(1e-5f));
        }
    }

    [Test]
    public void CorridorTakesTheWholeLineInsideWithoutSharingCells()
    {
        // Corridor one cell wide along z; a line of 3 by 2 does not fit across it.
        var probe = Probe(new float4(-0.5f, -20f, 0.5f, 20f));
        var requests = Line(3, 2, quaternion.identity);

        var results = Resolve(ref probe, float3.zero, quaternion.identity, requests);

        foreach (var result in results)
        {
            Assert.That(result.status, Is.Not.EqualTo(FormationSlotResolveStatus.Unresolved));
            Assert.That(math.abs(result.position.x), Is.LessThanOrEqualTo(0.5f));
        }
        Assert.That(results[1].status, Is.EqualTo(FormationSlotResolveStatus.Template)); // Centre column fits.
        Assert.That(results[0].status, Is.EqualTo(FormationSlotResolveStatus.Relocated));
        AssertNoSharedCells(results);

        // Deterministic: a second resolver gives the same answer.
        var again = Resolve(ref probe, float3.zero, quaternion.identity, requests);
        for (int i = 0; i < results.Count; i++)
            Assert.That(again[i].position, Is.EqualTo(results[i].position));
    }

    /// <summary>
    /// An even-width line sits on half cells (±0.5, ±1.5, ±2.5 spacings): with noise, rounding offsets to cell indices
    /// put two neighbours in one cell, which stacked soldiers in the S7 corridor.
    /// </summary>
    [Test]
    public void EvenWidthLineWithNoiseNeverSharesACell()
    {
        var probe = Probe(new float4(-0.5f, -20f, 0.5f, 20f));
        var requests = Line(6, 2, quaternion.identity);
        for (int i = 0; i < requests.Count; i++)
        {
            // Deterministic noise of up to 0.08 spacings, as the authored Line formation has.
            var request = requests[i];
            var noise = new float2(math.sin(i * 1.7f), math.cos(i * 2.3f)) * 0.08f * Spacing;
            request.localOffset += noise;
            request.template += new float3(noise.x, 0f, noise.y);
            requests[i] = request;
        }

        var results = Resolve(ref probe, float3.zero, quaternion.identity, requests);

        foreach (var result in results)
            Assert.That(result.status, Is.Not.EqualTo(FormationSlotResolveStatus.Unresolved));
        AssertNoSharedCells(results);
    }

    [Test]
    public void RotatedFormationRelocatesAlongItsOwnAxes()
    {
        // Corridor along world x; the formation faces +x, so its rows run across the corridor.
        var probe = Probe(new float4(-20f, -0.5f, 20f, 0.5f));
        var rotation = quaternion.RotateY(math.PI * 0.5f);
        var requests = Line(3, 2, rotation);

        var results = Resolve(ref probe, float3.zero, rotation, requests);

        foreach (var result in results)
        {
            Assert.That(result.status, Is.Not.EqualTo(FormationSlotResolveStatus.Unresolved));
            Assert.That(math.abs(result.position.z), Is.LessThanOrEqualTo(0.5f));
        }
        AssertNoSharedCells(results);
    }

    [Test]
    public void SlotsBehindAWallMoveToTheOriginSide()
    {
        // Ground on both sides of a wall at z = 0.5 (gap 0.3-0.7); the far side is walkable but not reachable straight.
        var probe = Probe(new float4(-20f, -20f, 20f, 0.3f), new float4(-20f, 0.7f, 20f, 20f));
        var requests = Line(3, 2, quaternion.identity, new float3(0f, 0f, 1f)); // Front row at z = 1, behind the wall.
        var origin = new float3(0f, 0f, -1f);

        var results = Resolve(ref probe, origin, quaternion.identity, requests);

        for (int i = 0; i < 3; i++)
        {
            Assert.That(results[i].status, Is.EqualTo(FormationSlotResolveStatus.Relocated), $"slot {i}");
            Assert.That(results[i].position.z, Is.LessThan(0.3f), $"slot {i}");
        }
        AssertNoSharedCells(results);
    }

    [Test]
    public void BlockedSlotKeepsItsPreviousCellWhileItIsStillFree()
    {
        var probe = Probe(new float4(-0.5f, -20f, 0.5f, 20f));
        var requests = Line(3, 1, quaternion.identity);
        var first = Resolve(ref probe, float3.zero, quaternion.identity, requests);
        // Pretend slot 0 had gone two cells further back last time: it stays there instead of the nearest cell.
        var request = requests[0];
        request.previousShift = first[0].shift + new int2(0, -2);
        requests[0] = request;

        var second = Resolve(ref probe, float3.zero, quaternion.identity, requests);

        Assert.That(second[0].shift, Is.EqualTo(request.previousShift));
        Assert.That(second[0].status, Is.EqualTo(FormationSlotResolveStatus.Relocated));
        AssertNoSharedCells(second);
    }

    [Test]
    public void WithoutAFreeCellTheSlotFallsBackToItsTemplate()
    {
        // Room for one soldier only.
        var probe = Probe(new float4(-0.3f, -0.3f, 0.3f, 0.3f));
        var requests = Line(3, 1, quaternion.identity);

        var results = Resolve(ref probe, float3.zero, quaternion.identity, requests);

        Assert.That(results[1].status, Is.EqualTo(FormationSlotResolveStatus.Template));
        Assert.That(results[0].status, Is.EqualTo(FormationSlotResolveStatus.Unresolved));
        Assert.That(results[0].position, Is.EqualTo(requests[0].template));
        Assert.That(results[2].status, Is.EqualTo(FormationSlotResolveStatus.Unresolved));
    }

    [Test]
    public void CellOfAnotherSquadCountsAsTaken()
    {
        var probe = Probe(OpenGround);
        var requests = Line(3, 1, quaternion.identity);
        var occupied = new List<float2> { requests[1].template.xz };
        var results = ResolveAround(ref probe, requests, occupied);

        Assert.That(results[0].status, Is.EqualTo(FormationSlotResolveStatus.Template));
        Assert.That(results[2].status, Is.EqualTo(FormationSlotResolveStatus.Template));
        Assert.That(results[1].status, Is.EqualTo(FormationSlotResolveStatus.Relocated));
        Assert.That(math.distance(results[1].position.xz, occupied[0]), Is.GreaterThanOrEqualTo(Spacing - 1e-3f));
        AssertNoSharedCells(results);
    }

    [Test]
    public void SearchSkipsCellsOfAnotherSquad()
    {
        var probe = Probe(OpenGround);
        var requests = Line(1, 1, quaternion.identity);
        float2 cell = requests[0].template.xz;
        var occupied = new List<float2>
        {
            cell, cell + new float2(Spacing, 0f), cell - new float2(Spacing, 0f),
            cell + new float2(0f, Spacing), cell - new float2(0f, Spacing)
        };
        var results = ResolveAround(ref probe, requests, occupied);

        Assert.That(results[0].status, Is.EqualTo(FormationSlotResolveStatus.Relocated));
        foreach (float2 taken in occupied)
            Assert.That(math.distance(results[0].position.xz, taken), Is.GreaterThanOrEqualTo(Spacing * 0.5f));
    }

    [Test]
    public void DistantOccupiedCellsChangeNothing()
    {
        var probe = Probe(OpenGround);
        var requests = Line(3, 1, quaternion.identity);
        var alone = ResolveAround(ref probe, requests, null);
        int queries = probe.queries;

        probe.queries = 0;
        var results = ResolveAround(ref probe, requests, new List<float2> { new(1000f, 1000f) });

        Assert.That(probe.queries, Is.EqualTo(queries));
        for (int i = 0; i < alone.Count; i++)
            Assert.That(results[i].position, Is.EqualTo(alone[i].position));
    }

    [Test]
    public void WithEveryCellOccupiedTheSlotFallsBackToItsTemplate()
    {
        var probe = Probe(OpenGround);
        var requests = Line(1, 1, quaternion.identity);
        var occupied = new List<float2>();
        for (int x = -SearchCells; x <= SearchCells; x++)
        for (int y = -SearchCells; y <= SearchCells; y++)
            occupied.Add(requests[0].template.xz + new float2(x, y) * Spacing);
        var results = ResolveAround(ref probe, requests, occupied);

        Assert.That(results[0].status, Is.EqualTo(FormationSlotResolveStatus.Unresolved));
        Assert.That(results[0].position, Is.EqualTo(requests[0].template));
    }

    [Test]
    public void FootprintsAreNearOnlyWithinTheMargin()
    {
        var a = FormationSlotFootprint.Empty;
        a.Include(new float3(0f, 0f, 0f));
        a.Include(new float3(4f, 0f, 2f));
        var b = FormationSlotFootprint.Empty;
        b.Include(new float3(5f, 0f, 1f));

        Assert.That(a.Near(b, 1.5f), Is.True);
        Assert.That(b.Near(a, 1.5f), Is.True);
        Assert.That(a.Near(b, 0.5f), Is.False);
        Assert.That(FormationSlotFootprint.Empty.Near(a, 1000f), Is.False);
    }

    [Test]
    public void OriginFromTheLeaderStopsAtTheWall()
    {
        var probe = Probe(new float4(-20f, -20f, 20f, 0.3f), new float4(-20f, 0.7f, 20f, 20f));
        var leader = new float3(0f, 0f, -1f);
        var center = new float3(0f, 0f, 1f); // Anchor beyond the wall.

        Assert.That(FormationSlotResolver.TryOrigin(ref probe, center, true, leader, 2f, out float3 origin), Is.True);
        Assert.That(origin.z, Is.LessThanOrEqualTo(0.3f));

        // Without a leader the centre is simply projected, here on the far side.
        Assert.That(FormationSlotResolver.TryOrigin(ref probe, center, false, leader, 2f, out origin), Is.True);
        Assert.That(origin.z, Is.GreaterThanOrEqualTo(0.7f));
    }
}
