using System.Collections.Generic;
using ConquestTactics.Animation;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves Unity trigger contacts against current ECS attack state and creates
/// disposable damage events. A shield contact wins over a body contact on the
/// same target within one queue drain.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(DamageCalculationSystem))]
public partial class MeleeContactResolutionSystem : SystemBase
{
    private readonly Dictionary<Entity, MeleeContact> _contactsByAttacker = new();
    private EntityArchetype _damageEventArchetype;

    protected override void OnCreate()
        => _damageEventArchetype = EntityManager.CreateArchetype(typeof(PendingDamageEvent));

    protected override void OnUpdate()
    {
        int pendingCount = MeleeContactQueue.Pending.Count;
        if (pendingCount == 0) return;
        var em = EntityManager;
        var contactsByAttacker = _contactsByAttacker;
        contactsByAttacker.Clear();
        for (int i = 0; i < pendingCount && MeleeContactQueue.Pending.TryDequeue(out var contact); i++)
        {
            if (contact.world != World)
            {
                if (contact.world != null && contact.world.IsCreated)
                    MeleeContactQueue.Pending.Enqueue(contact);
                continue;
            }

            // Ignore non-candidates before choosing the first target for this swing.
            if (!em.Exists(contact.attacker) || !em.Exists(contact.target)
                || contact.attacker == contact.target
                || em.HasComponent<IsDeadComponent>(contact.attacker)
                || (em.HasComponent<HeroLifeComponent>(contact.attacker)
                    && !em.GetComponentData<HeroLifeComponent>(contact.attacker).isAlive)
                || em.HasComponent<IsDeadComponent>(contact.target))
                continue;
            if (em.HasComponent<TeamComponent>(contact.attacker)
                && em.HasComponent<TeamComponent>(contact.target))
            {
                Team attackerTeam = em.GetComponentData<TeamComponent>(contact.attacker).value;
                if (attackerTeam != Team.None
                    && attackerTeam == em.GetComponentData<TeamComponent>(contact.target).value)
                    continue;
            }

            if (!contactsByAttacker.TryGetValue(contact.attacker, out var first))
                contactsByAttacker.Add(contact.attacker, contact);
            else if (first.target == contact.target && contact.hitType == HitType.Shield)
                contactsByAttacker[contact.attacker] = contact;
        }

        foreach (var contact in contactsByAttacker.Values)
        {
            if (!em.Exists(contact.attacker) || !em.Exists(contact.target)
                || contact.attacker == contact.target
                || !em.HasComponent<WeaponHitboxActiveTag>(contact.attacker)
                || !em.IsComponentEnabled<WeaponHitboxActiveTag>(contact.attacker)
                || em.HasComponent<IsDeadComponent>(contact.target))
                continue;

            bool isHero = em.HasComponent<HeroCombatComponent>(contact.attacker);
            if (!isHero && !em.HasComponent<UnitCombatComponent>(contact.attacker))
                continue;
            if (isHero && em.GetComponentData<HeroCombatComponent>(contact.attacker).hitboxFired)
                continue;
            if (!isHero && em.GetComponentData<UnitCombatComponent>(contact.attacker).hitboxFired)
                continue;

            Team sourceTeam = em.HasComponent<TeamComponent>(contact.attacker)
                ? em.GetComponentData<TeamComponent>(contact.attacker).value : Team.None;
            if (sourceTeam != Team.None && em.HasComponent<TeamComponent>(contact.target)
                && em.GetComponentData<TeamComponent>(contact.target).value == sourceTeam)
                continue;

            Entity damageProfile;
            float criticalChance;
            float criticalMultiplier;
            float attackerSpeed = 0f;
            if (isHero)
            {
                var combat = em.GetComponentData<HeroCombatComponent>(contact.attacker);
                damageProfile = contact.attacker;
                criticalChance = combat.criticalChance;
                criticalMultiplier = combat.criticalMultiplier;
            }
            else
            {
                if (!em.HasComponent<UnitWeaponComponent>(contact.attacker)) continue;
                var weapon = em.GetComponentData<UnitWeaponComponent>(contact.attacker);
                damageProfile = weapon.damageProfile;
                criticalChance = weapon.criticalChance;
                criticalMultiplier = weapon.criticalMultiplier;
                if (em.HasComponent<UnitAnimationMovementComponent>(contact.attacker))
                    attackerSpeed = em.GetComponentData<UnitAnimationMovementComponent>(contact.attacker).CurrentSpeed;
            }
            if (!em.Exists(damageProfile) || !em.HasComponent<DamageProfileComponent>(damageProfile))
                continue;

            bool critical = Random.value <= criticalChance;
            var eventEntity = em.CreateEntity(_damageEventArchetype);
            em.SetComponentData(eventEntity, new PendingDamageEvent
            {
                destroyEntityAfterProcessing = true,
                target = contact.target,
                damageSource = contact.attacker,
                damageProfile = damageProfile,
                hasInlineDamageProfile = true,
                inlineDamageProfile = em.GetComponentData<DamageProfileComponent>(damageProfile),
                sourceTeam = sourceTeam,
                category = critical ? DamageCategory.Critical : DamageCategory.Normal,
                multiplier = critical ? criticalMultiplier : 1f,
                attackerSpeed = attackerSpeed,
                attackerPosition = contact.attackerPosition,
                hitType = contact.hitType
            });

            if (isHero)
            {
                var combat = em.GetComponentData<HeroCombatComponent>(contact.attacker);
                combat.hitboxFired = true;
                em.SetComponentData(contact.attacker, combat);
            }
            else
            {
                var combat = em.GetComponentData<UnitCombatComponent>(contact.attacker);
                combat.hitboxFired = true;
                em.SetComponentData(contact.attacker, combat);
            }

            HitImpactEffectManager.Instance?.Spawn(contact.contactPoint);
        }
    }
}
