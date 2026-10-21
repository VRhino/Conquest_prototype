using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies the effect events of skills (Aprendizajes_Referencia.md Fase 2, generic effects) and destroys the requests:
/// <list type="bullet">
/// <item>area damage: a <see cref="PendingDamageEvent"/> with the caster's damage profile × the event's value for every
/// living enemy in the circle or cone (category Ability), resolved by <see cref="DamageCalculationSystem"/>;</item>
/// <item>modifier: a timed modifier on the stack of each target (<see cref="StatEffects.ApplyTimed"/>);</item>
/// <item>absorb shield: <see cref="AbsorbShieldComponent"/> on each target (the larger one stays);</item>
/// <item>dash: <see cref="HeroDashComponent"/> on the caster, which hero movement follows.</item>
/// </list>
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SkillExecutionSystem))]
public partial class SkillEffectSystem : SystemBase
{
    EntityQuery _requests;
    EntityQuery _bodies;

    protected override void OnCreate()
    {
        _requests = GetEntityQuery(ComponentType.ReadOnly<SkillEffectRequestComponent>());
        _bodies = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<TeamComponent>() },
            Any = new[] { ComponentType.ReadOnly<HealthComponent>(), ComponentType.ReadOnly<HeroHealthComponent>() },
            None = new[] { ComponentType.ReadOnly<IsDeadComponent>() },
        });
        RequireForUpdate(_requests);
    }

    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        using var requests = _requests.ToComponentDataArray<SkillEffectRequestComponent>(Allocator.Temp);
        using var bodies = _bodies.ToEntityArray(Allocator.Temp);
        using var positions = _bodies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var teams = _bodies.ToComponentDataArray<TeamComponent>(Allocator.Temp);
        EntityManager.DestroyEntity(_requests);

        var targets = new NativeList<Entity>(32, Allocator.Temp);
        foreach (var r in requests)
        {
            if (!EntityManager.Exists(r.caster)) continue;
            var e = r.effect;
            switch (e.type)
            {
                case SkillEventType.AreaDamage:
                    DealAreaDamage(r, bodies, positions, teams);
                    break;
                case SkillEventType.ApplyModifier:
                case SkillEventType.AbsorbShield:
                    targets.Clear();
                    SelectTargets(r, bodies, positions, teams, targets);
                    foreach (var target in targets)
                    {
                        if (e.type == SkillEventType.ApplyModifier) ApplyModifier(target, r, now);
                        else ApplyShield(target, e, now);
                    }
                    break;
                case SkillEventType.Dash:
                    if (e.duration <= 0f) break;
                    var dash = new HeroDashComponent { direction = r.forward, speed = e.distance / e.duration, endTime = now + e.duration };
                    if (EntityManager.HasComponent<HeroDashComponent>(r.caster)) EntityManager.SetComponentData(r.caster, dash);
                    else EntityManager.AddComponentData(r.caster, dash);
                    break;
            }
        }
        targets.Dispose();
    }

    void DealAreaDamage(in SkillEffectRequestComponent r, NativeArray<Entity> bodies, NativeArray<LocalTransform> positions,
        NativeArray<TeamComponent> teams)
    {
        if (!EntityManager.HasComponent<DamageProfileComponent>(r.caster)) return;
        var profile = EntityManager.GetComponentData<DamageProfileComponent>(r.caster);
        float3 centre = SkillArea.Centre(r.origin, r.forward, r.effect.distance);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (teams[i].value == r.team || bodies[i] == r.caster) continue;
            if (!SkillArea.Contains(centre, r.forward, r.effect.radius, r.effect.angle, positions[i].Position)) continue;
            var hit = EntityManager.CreateEntity(typeof(PendingDamageEvent));
            EntityManager.SetComponentData(hit, new PendingDamageEvent
            {
                destroyEntityAfterProcessing = true,
                target = bodies[i],
                damageSource = r.caster,
                damageProfile = r.caster,
                hasInlineDamageProfile = true,
                inlineDamageProfile = profile,
                sourceTeam = r.team,
                category = DamageCategory.Ability,
                multiplier = r.effect.value,
                attackerPosition = r.origin,
                hitType = HitType.Body,
            });
        }
    }

    void SelectTargets(in SkillEffectRequestComponent r, NativeArray<Entity> bodies, NativeArray<LocalTransform> positions,
        NativeArray<TeamComponent> teams, NativeList<Entity> targets)
    {
        switch (r.effect.target)
        {
            case SkillTarget.Self:
                targets.Add(r.caster);
                break;
            case SkillTarget.OwnSquad:
                if (!EntityManager.HasComponent<HeroSquadReference>(r.caster)) break;
                var squad = EntityManager.GetComponentData<HeroSquadReference>(r.caster).squad;
                if (!EntityManager.Exists(squad) || !EntityManager.HasBuffer<SquadUnitElement>(squad)) break;
                foreach (var unit in EntityManager.GetBuffer<SquadUnitElement>(squad))
                    if (EntityManager.Exists(unit.Value)) targets.Add(unit.Value);
                break;
            case SkillTarget.AlliesInRadius:
            case SkillTarget.EnemiesInRadius:
                bool allies = r.effect.target == SkillTarget.AlliesInRadius;
                float3 centre = allies ? r.origin : SkillArea.Centre(r.origin, r.forward, r.effect.distance);
                for (int i = 0; i < bodies.Length; i++)
                {
                    if ((teams[i].value == r.team) != allies) continue;
                    if (SkillArea.Contains(centre, r.forward, r.effect.radius, 0f, positions[i].Position)) targets.Add(bodies[i]);
                }
                if (allies && !targets.Contains(r.caster)) targets.Add(r.caster);
                break;
        }
    }

    void ApplyModifier(Entity target, in SkillEffectRequestComponent r, double now)
    {
        if (!EntityManager.HasBuffer<StatModifier>(target)) return;
        var e = r.effect;
        StatEffects.ApplyTimed(EntityManager.GetBuffer<StatModifier>(target), e.stat, e.op, e.value, now + e.duration, r.skillId);
        if (EntityManager.HasComponent<ModifiersDirty>(target)) EntityManager.SetComponentEnabled<ModifiersDirty>(target, true);
    }

    void ApplyShield(Entity target, in SkillEvent e, double now)
    {
        var shield = new AbsorbShieldComponent { value = e.value, expiresAt = e.duration > 0f ? now + e.duration : 0 };
        if (!EntityManager.HasComponent<AbsorbShieldComponent>(target))
            EntityManager.AddComponentData(target, shield);
        else if (!AbsorbShield.IsActive(EntityManager.GetComponentData<AbsorbShieldComponent>(target), now)
                 || EntityManager.GetComponentData<AbsorbShieldComponent>(target).value < shield.value)
            EntityManager.SetComponentData(target, shield);
    }
}
