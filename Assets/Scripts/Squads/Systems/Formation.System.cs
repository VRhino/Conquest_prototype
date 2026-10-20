using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Single writer of the formation layout of each squad (<see cref="FormationComponent"/>) and of every unit slot
/// (<see cref="UnitGridSlotComponent"/>). Rebuilds the layout when the player changes formation (with cooldown),
/// when the number of soldiers alive calls for another template, and when a spacing request arrives.
/// A new layout (formation or template change) deals each unit the nearest free slot (greedy by distance, only on
/// the event), so units do not cross the whole formation; otherwise every unit keeps its slot.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadOrderSystem))]
public partial class FormationSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float formationChangeCooldown = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>().formationChangeCooldown
            : MovementTuningComponent.Default.formationChangeCooldown;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (resolved, state, formationComp, squadDef, units, squadEntity) in SystemAPI
                    .Query<RefRO<SquadResolvedOrderComponent>,
                            RefRW<SquadStateComponent>,
                            RefRW<FormationComponent>,
                            RefRO<SquadDefinitionComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                    .WithEntityAccess())
        {
            var s = state.ValueRW;
            s.formationChangeCooldown = math.max(0f, s.formationChangeCooldown - deltaTime);
            var current = formationComp.ValueRO;

            if (!squadDef.ValueRO.formationLibrary.IsCreated || units.Length == 0
                || !SystemAPI.HasComponent<SquadFormationAnchorComponent>(squadEntity))
            {
                state.ValueRW = s;
                continue;
            }
            ref var library = ref squadDef.ValueRO.formationLibrary.Value;

            bool spacingRequested = SystemAPI.HasComponent<SquadFormationSpacingRequest>(squadEntity);
            float scale = current.scale > 0f ? current.scale : 1f;
            if (spacingRequested)
            {
                scale = math.max(0.01f, SystemAPI.GetComponent<SquadFormationSpacingRequest>(squadEntity).scale);
                ecb.RemoveComponent<SquadFormationSpacingRequest>(squadEntity);
            }

            bool formationRequested = resolved.ValueRO.formation != current.currentFormation
                && s.formationChangeCooldown <= 0f;
            FormationType type = formationRequested ? resolved.ValueRO.formation : current.currentFormation;
            int formationIndex = FormationLayout.Find(ref library, type);
            if (formationIndex < 0)
            {
                // Unknown formation: keep the current layout, but still apply a spacing request to it.
                formationRequested = false;
                type = current.currentFormation;
                formationIndex = FormationLayout.Find(ref library, type);
                if (formationIndex < 0)
                {
                    state.ValueRW = s;
                    continue;
                }
            }
            ref var formation = ref library.formations[formationIndex];
            if (FormationLayout.Capacity(ref formation) < units.Length)
            {
                // Do not commit a layout that cannot place every unit.
                state.ValueRW = s;
                continue;
            }

            int templateIndex = FormationLayout.SelectTemplate(ref formation, units.Length);
            bool templateChanged = templateIndex != current.templateIndex || units.Length != current.layoutUnitCount
                && formation.layout == FormationLayoutKind.Scatter;
            if (!formationRequested && !spacingRequested && !templateChanged)
            {
                state.ValueRW = s;
                continue;
            }

            float spacing = FormationLayout.EffectiveSpacing(formation.spacing, scale);
            uint seed = current.layoutSeed;
            var anchor = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squadEntity);
            SquadHoldPositionComponent? holdComponent = SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity)
                ? SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity) : null;
            bool redeal = formationRequested || templateChanged;
            var slotOf = new NativeArray<int>(units.Length, Allocator.Temp);
            if (redeal)
                AssignNearestSlots(ref formation, templateIndex, spacing, seed, units, s, holdComponent, anchor, slotOf);
            else
                for (int i = 0; i < units.Length; i++)
                    slotOf[i] = SystemAPI.HasComponent<UnitGridSlotComponent>(units[i].Value)
                        ? SystemAPI.GetComponent<UnitGridSlotComponent>(units[i].Value).slotIndex : i;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit)) continue;
                if (!FormationPositionCalculator.TryBuildSlot(ref formation, templateIndex, slotOf[i], spacing, seed,
                        out var slot))
                    continue;

                if (SystemAPI.HasComponent<UnitGridSlotComponent>(unit))
                    SystemAPI.SetComponent(unit, slot);
                else
                    ecb.AddComponent(unit, slot);

                float3 worldPos = FormationPositionCalculator.DesiredPosition(slot.localOffset, s, holdComponent,
                    anchor.position, anchor.rotation, true);
                if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                    SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit).ValueRW.position = worldPos;
                else
                    ecb.AddComponent(unit, new UnitTargetPositionComponent { position = worldPos });
            }

            formationComp.ValueRW = new FormationComponent
            {
                currentFormation = type,
                templateIndex = templateIndex,
                layoutUnitCount = units.Length,
                spacing = spacing,
                scale = scale,
                layoutSeed = seed
            };

            if (formationRequested)
            {
                s.formationChangeCooldown = formationChangeCooldown;
                // Hold Position: keep the same centre so the new formation is laid out in place.
                if (s.currentOrder == SquadOrderType.HoldPosition && SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
                    SystemAPI.GetComponentRW<SquadHoldPositionComponent>(squadEntity).ValueRW.originalFormation = type;
            }

            state.ValueRW = s;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    readonly List<(float distSq, int unit, int slot)> _pairs = new();

    /// <summary>
    /// Greedy nearest assignment (Movimiento_Formaciones.md L-2): every unit/slot pair sorted by distance, taken while
    /// both are free. Units without a position take the remaining slots in buffer order. N ≤ ~30, so N² on the event.
    /// </summary>
    void AssignNearestSlots(ref FormationDataBlob formation, int templateIndex, float spacing, uint seed,
        DynamicBuffer<SquadUnitElement> units, in SquadStateComponent state, SquadHoldPositionComponent? hold,
        in SquadFormationAnchorComponent anchor, NativeArray<int> slotOf)
    {
        int n = units.Length;
        float3 center = FormationPositionCalculator.GetSquadCenter(state, hold, anchor.position);
        quaternion inverse = math.lengthsq(anchor.rotation.value) > 1e-6f
            ? math.inverse(math.normalize(anchor.rotation)) : quaternion.identity;

        _pairs.Clear();
        for (int u = 0; u < n; u++)
        {
            slotOf[u] = -1;
            Entity unit = units[u].Value;
            if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<LocalTransform>(unit)) continue;
            float2 local = math.mul(inverse, SystemAPI.GetComponent<LocalTransform>(unit).Position - center).xz;
            for (int k = 0; k < n; k++)
                if (FormationPositionCalculator.TryBuildSlot(ref formation, templateIndex, k, spacing, seed, out var slot))
                    _pairs.Add((math.distancesq(local, slot.localOffset.xz), u, k));
        }
        _pairs.Sort((a, b) => a.distSq.CompareTo(b.distSq));

        var taken = new NativeArray<bool>(n, Allocator.Temp);
        foreach (var (_, unit, slot) in _pairs)
        {
            if (slotOf[unit] >= 0 || taken[slot]) continue;
            slotOf[unit] = slot;
            taken[slot] = true;
        }
        int next = 0;
        for (int u = 0; u < n; u++)
        {
            if (slotOf[u] >= 0) continue;
            while (next < n && taken[next]) next++;
            slotOf[u] = next < n ? next : u;
            if (next < n) taken[next] = true;
        }
    }
}
