using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Contact ring (Movimiento_Formaciones.md L-7, C11): every melee target gets a ring of positions at the stop
/// distance and each attacker reserves one, keeping it while its target stays the same. Attackers that find the
/// ring full wait further out (D6, soft saturation: no hard cap here, targeting keeps its own). Single writer
/// of <see cref="MeleeContactSlotComponent"/>; reservations are rebuilt every frame from the attackers alive, so
/// death or a new target frees a slot without any event.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitTargetingSystem))]
[UpdateBefore(typeof(UnitNavMeshSystem))]
public partial class MeleeContactRingSystem : SystemBase
{
    struct Attacker
    {
        public Entity unit, target;
        public float2 position, targetPosition;
        public float stopDistance, distanceSq;
        public bool keepsSlot;
    }

    struct Ring
    {
        public float radius;
        public int count;
        public ulong occupied;
    }

    readonly List<Attacker> _attackers = new();
    readonly List<int> _pending = new();
    readonly Dictionary<Entity, Ring> _rings = new();
    readonly List<Entity> _ringKeys = new();

    protected override void OnUpdate()
    {
        var tuning = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default;
        float bodyRadius = tuning.bodyRadius;
        float arcSpacing = 2f * bodyRadius + tuning.contactRingMargin;
        float maxArc = math.radians(tuning.contactRingMaxArcDegrees);
        bool enabled = tuning.contactRingEnabled;

        // 1. Melee attackers with a living target; everyone else drops its reservation.
        _attackers.Clear();
        _rings.Clear();
        foreach (var (combat, weapon, transform, slot, unit) in SystemAPI
                     .Query<RefRO<UnitCombatComponent>, RefRO<UnitWeaponComponent>, RefRO<LocalTransform>,
                            RefRW<MeleeContactSlotComponent>>()
                     .WithNone<UnitRangedStatsComponent, IsDeadComponent>()
                     .WithEntityAccess())
        {
            Entity target = combat.ValueRO.target;
            if (!enabled || target == Entity.Null || !SystemAPI.Exists(target) || !SystemAPI.HasComponent<LocalTransform>(target)
                || SystemAPI.HasComponent<IsDeadComponent>(target))
            {
                slot.ValueRW = new MeleeContactSlotComponent { target = Entity.Null, slot = -1 };
                continue;
            }
            float2 position = transform.ValueRO.Position.xz;
            float2 targetPosition = SystemAPI.GetComponent<LocalTransform>(target).Position.xz;
            float stop = MeleeReach.StopDistance(weapon.ValueRO.attackRange, bodyRadius, bodyRadius,
                tuning.meleeStopReachFraction);
            _attackers.Add(new Attacker
            {
                unit = unit, target = target, position = position, targetPosition = targetPosition,
                stopDistance = stop, distanceSq = math.distancesq(position, targetPosition),
            });
            // The ring sits at the smallest stop distance, so every attacker on it can still hit.
            _rings[target] = _rings.TryGetValue(target, out var ring)
                ? new Ring { radius = math.min(ring.radius, stop) }
                : new Ring { radius = stop };
        }
        if (_attackers.Count == 0) return;

        _ringKeys.Clear();
        _ringKeys.AddRange(_rings.Keys);
        foreach (var key in _ringKeys)
        {
            var ring = _rings[key];
            ring.count = ContactRing.SlotCount(ring.radius, arcSpacing);
            _rings[key] = ring;
        }

        // 2. Keep the reservations that still hold (same target, same ring); the rest choose below.
        _pending.Clear();
        for (int i = 0; i < _attackers.Count; i++)
        {
            var a = _attackers[i];
            var previous = SystemAPI.GetComponent<MeleeContactSlotComponent>(a.unit);
            var ring = _rings[a.target];
            if (previous.target == a.target && previous.slotCount == ring.count
                && previous.slot >= 0 && previous.slot < ring.count && (ring.occupied & (1UL << previous.slot)) == 0)
            {
                ring.occupied |= 1UL << previous.slot;
                _rings[a.target] = ring;
                a.keepsSlot = true;
                _attackers[i] = a;
            }
            else _pending.Add(i);
        }

        // 3. Nearest attackers choose first: the free slot closest to the side they come from.
        _pending.Sort((x, y) => _attackers[x].distanceSq.CompareTo(_attackers[y].distanceSq));
        foreach (int i in _pending)
        {
            var a = _attackers[i];
            var ring = _rings[a.target];
            int chosen = ContactRing.NearestFreeSlot(ContactRing.Bearing(a.targetPosition, a.position),
                ring.count, ring.occupied, maxArc);
            if (chosen >= 0)
            {
                ring.occupied |= 1UL << chosen;
                _rings[a.target] = ring;
            }
            SystemAPI.SetComponent(a.unit, new MeleeContactSlotComponent
            {
                target = a.target, slot = chosen, slotCount = ring.count, radius = ring.radius,
            });
        }
        foreach (var a in _attackers)
        {
            if (!a.keepsSlot) continue;
            var kept = SystemAPI.GetComponent<MeleeContactSlotComponent>(a.unit);
            kept.radius = _rings[a.target].radius;
            SystemAPI.SetComponent(a.unit, kept);
        }
    }
}
