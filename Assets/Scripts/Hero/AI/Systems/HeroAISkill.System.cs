using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// The AI's use of skills (H1) and its choice of an ally to support (L4-08), on each think of the hero
/// (<see cref="AIThinkClock"/>): after the behavior wrote its decision, adds the class skill to launch and the squad
/// ability to use (<see cref="HeroAIDecision.useSkill"/>, <see cref="HeroAIDecision.useSquadSkill"/>), which
/// <see cref="HeroSkillRequestSystem"/> turns into requests through the same gate as the player's.
/// <list type="bullet">
/// <item>Ultimate, R, E, Q in that order; only skills the gate would let through now (cooldown and stamina, the same
/// inputs it checks), so the AI does not keep asking for what is cooling down.</item>
/// <item>Offensive skills with an enemy hero or enough soldiers in their reach; skills on self or squad while fighting;
/// skills on allies in a radius when the chosen ally is in reach and hurt (<see cref="HeroAISkills"/>).</item>
/// <item>The squad's first ready ability while the squad fights.</item>
/// </list>
/// </summary>
[ServerAuthoritative] // Decides the match (L4-06)
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIRusherSystem))]
[UpdateAfter(typeof(HeroAIBalancedSystem))]
[UpdateBefore(typeof(HeroSkillRequestSystem))]
public partial class HeroAISkillSystem : SystemBase
{
    static readonly SkillSlot[] HeroSlots = { SkillSlot.Ultimate, SkillSlot.R, SkillSlot.E, SkillSlot.Q };

    EntityQuery _heroes;
    EntityQuery _bodies;

    struct Enemy
    {
        public float distance;
        public bool isHero;
    }

    protected override void OnCreate()
    {
        _heroes = GetEntityQuery(ComponentType.ReadOnly<HeroAITag>(), ComponentType.ReadWrite<HeroAIDecision>(),
            ComponentType.ReadOnly<HeroSkillElement>(), ComponentType.ReadOnly<CooldownGroupElement>(),
            ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<TeamComponent>());
        _bodies = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<TeamComponent>() },
            Any = new[] { ComponentType.ReadOnly<HealthComponent>(), ComponentType.ReadOnly<HeroHealthComponent>() },
            None = new[] { ComponentType.ReadOnly<IsDeadComponent>() },
        });
        RequireForUpdate(_heroes);
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.GetSingleton<MatchStateComponent>().currentState != MatchState.InBattle) return;
        double now = SystemAPI.Time.ElapsedTime;
        var tuning = SystemAPI.TryGetSingleton<SquadSpawnConfigComponent>(out var config)
            ? config.heroAISkills.OrDefault() : HeroAISkillTuning.Default;

        using var heroes = _heroes.ToEntityArray(Allocator.Temp);
        NativeArray<Entity> bodies = default;
        NativeArray<LocalTransform> poses = default;
        NativeArray<TeamComponent> teams = default;
        var enemies = new NativeList<Enemy>(64, Allocator.Temp);
        var allies = new NativeList<AllyCandidate>(64, Allocator.Temp);
        foreach (var hero in heroes)
        {
            var bb = EntityManager.GetComponentObject<HeroAIBlackboard>(hero);
            if (bb == null || bb.lastThinkTime != now || !bb.selfIsAlive) continue; // Only on its think.
            if (!bodies.IsCreated)
            {
                bodies = _bodies.ToEntityArray(Allocator.Temp);
                poses = _bodies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                teams = _bodies.ToComponentDataArray<TeamComponent>(Allocator.Temp);
            }

            float3 self = EntityManager.GetComponentData<LocalTransform>(hero).Position;
            var team = EntityManager.GetComponentData<TeamComponent>(hero).value;
            enemies.Clear();
            allies.Clear();
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == hero) continue;
                float distance = math.distance(poses[i].Position.xz, self.xz);
                bool isHero = EntityManager.HasComponent<HeroHealthComponent>(bodies[i]);
                if (teams[i].value != team) enemies.Add(new Enemy { distance = distance, isHero = isHero });
                else if (distance <= tuning.allySearchRadius)
                    allies.Add(new AllyCandidate { entity = bodies[i], distance = distance, missingHealth = MissingHealth(bodies[i]), isHero = isHero });
            }

            Entity current = EntityManager.HasComponent<HeroAISupportTarget>(hero) ? EntityManager.GetComponentData<HeroAISupportTarget>(hero).ally : Entity.Null;
            Entity ally = HeroAISkills.PickAlly(allies.AsArray().AsReadOnlySpan(), current, tuning, out var picked);
            if (EntityManager.HasComponent<HeroAISupportTarget>(hero)) EntityManager.SetComponentData(hero, new HeroAISupportTarget { ally = ally });
            else EntityManager.AddComponentData(hero, new HeroAISupportTarget { ally = ally });

            var decision = EntityManager.GetComponentData<HeroAIDecision>(hero);
            bool fighting = bb.isUnderAttack || bb.squadIsInCombat;
            decision.useSkill = PickHeroSkill(hero, now, fighting, ally != Entity.Null, picked, enemies, tuning, out decision.skillSlot);
            decision.useSquadSkill = bb.squadIsInCombat && PickSquadSkill(bb.ownSquadEntity, now, out decision.squadSkillSlot);
            EntityManager.SetComponentData(hero, decision);
        }
        enemies.Dispose();
        allies.Dispose();
        if (bodies.IsCreated)
        {
            bodies.Dispose();
            poses.Dispose();
            teams.Dispose();
        }
    }

    float MissingHealth(Entity body)
    {
        if (EntityManager.HasComponent<HeroHealthComponent>(body))
        {
            var h = EntityManager.GetComponentData<HeroHealthComponent>(body);
            return h.maxHealth > 0f ? 1f - h.currentHealth / h.maxHealth : 0f;
        }
        var u = EntityManager.GetComponentData<HealthComponent>(body);
        return u.maxHealth > 0f ? 1f - u.currentHealth / u.maxHealth : 0f;
    }

    bool PickHeroSkill(Entity hero, double now, bool fighting, bool hasAlly, in AllyCandidate ally, NativeList<Enemy> enemies,
        in HeroAISkillTuning tuning, out SkillSlot slot)
    {
        var skills = EntityManager.GetBuffer<HeroSkillElement>(hero, true);
        var groups = EntityManager.GetBuffer<CooldownGroupElement>(hero, true);
        float stamina = EntityManager.HasComponent<StaminaComponent>(hero) ? EntityManager.GetComponentData<StaminaComponent>(hero).currentStamina : 0f;
        foreach (var candidate in HeroSlots)
        {
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i].slot != candidate || !skills[i].skill.IsCreated) continue;
                ref var skill = ref skills[i].skill.Value;
                if (!CooldownUtility.IsReady(groups, skill.cooldownGroup, now) || stamina < skill.staminaCost) break;
                var purpose = HeroAISkills.PurposeOf(ref skill, out float reach);
                int heroesInReach = 0, unitsInReach = 0;
                foreach (var e in enemies)
                {
                    if (e.distance > reach) continue;
                    if (e.isHero) heroesInReach++;
                    else unitsInReach++;
                }
                bool allyInReach = hasAlly && ally.distance <= reach;
                if (HeroAISkills.ShouldCast(purpose, heroesInReach, unitsInReach, fighting, allyInReach, ally.missingHealth, tuning))
                {
                    slot = candidate;
                    return true;
                }
                break;
            }
        }
        slot = default;
        return false;
    }

    bool PickSquadSkill(Entity squad, double now, out SkillSlot slot)
    {
        slot = default;
        if (!EntityManager.Exists(squad) || !EntityManager.HasBuffer<HeroSkillElement>(squad) || !EntityManager.HasBuffer<CooldownGroupElement>(squad))
            return false;
        var skills = EntityManager.GetBuffer<HeroSkillElement>(squad, true);
        var groups = EntityManager.GetBuffer<CooldownGroupElement>(squad, true);
        for (int i = 0; i < skills.Length; i++)
        {
            if (!skills[i].skill.IsCreated || !CooldownUtility.IsReady(groups, skills[i].skill.Value.cooldownGroup, now)) continue;
            slot = skills[i].slot;
            return true;
        }
        return false;
    }
}
