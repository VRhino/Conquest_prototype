using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles capture and interaction logic for <see cref="ZoneType.Supply"/> zones.
/// Heroes can capture the zone for their team and receive benefits when owned.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SupplyInteractionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;


        var healthLookup = GetComponentLookup<HealthComponent>();
        var staminaLookup = GetComponentLookup<StaminaComponent>();
        var interactionLookup = GetComponentLookup<PlayerInteractionComponent>(true);

        foreach (var (zone, supply, transform, entity) in
                 SystemAPI.Query<RefRW<ZoneTriggerComponent>,
                                   RefRW<SupplyPointComponent>,
                                   RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            if (zone.ValueRO.zoneType != ZoneType.Supply || !zone.ValueRO.isActive)
                continue;

            bool teamA = false;
            bool teamB = false;
            var alliedHeroes = new NativeList<Entity>(Allocator.Temp);

            float radiusSq = zone.ValueRO.radius * zone.ValueRO.radius;
            Team owner = (Team)supply.ValueRO.currentTeam;

            foreach (var (hTransform, life, team, heroEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>,
                                     RefRO<HeroLifeComponent>,
                                     RefRO<TeamComponent>>()
                             .WithEntityAccess())
            {
                if (!life.ValueRO.isAlive)
                    continue;

                if (math.distancesq(hTransform.ValueRO.Position, transform.ValueRO.Position) > radiusSq)
                    continue;

                if (team.ValueRO.value == Team.TeamA)
                    teamA = true;
                if (team.ValueRO.value == Team.TeamB)
                    teamB = true;

                if (team.ValueRO.value == owner)
                    alliedHeroes.Add(heroEntity);
            }

            bool contested = teamA && teamB;
            supply.ValueRW.isContested = contested;

            if (contested || (!teamA && !teamB))
            {
                alliedHeroes.Dispose();
                continue;
            }

            Team presentTeam = teamA ? Team.TeamA : Team.TeamB;
            int teamInt = (int)presentTeam;

            if (owner == presentTeam)
            {
                // Zone owned by present team - apply benefits
                foreach (var hero in alliedHeroes)
                {
                    if (healthLookup.HasComponent(hero))
                    {
                        var hp = healthLookup[hero];
                        hp.currentHealth = math.min(hp.maxHealth, hp.currentHealth + 25f * dt);
                        healthLookup[hero] = hp;
                    }

                    if (staminaLookup.HasComponent(hero))
                    {
                        var st = staminaLookup[hero];
                        st.currentStamina = math.min(st.maxStamina, st.currentStamina + st.regenRate * dt);
                        staminaLookup[hero] = st;
                    }

                    if (interactionLookup.HasComponent(hero) && interactionLookup[hero].interactPressed &&
                        !EntityManager.HasComponent<SquadSwapRequest>(hero))
                    {
                        EntityManager.AddComponentData(hero, new SquadSwapRequest { zoneId = zone.ValueRO.zoneId });
                    }
                }
            }
            else
            {
                // Capture attempt
                supply.ValueRW.captureProgress = math.min(100f, supply.ValueRO.captureProgress + supply.ValueRO.captureSpeed * dt);
                if (supply.ValueRO.captureProgress >= 100f)
                {
                    supply.ValueRW.captureProgress = 0f;
                    supply.ValueRW.currentTeam = teamInt;
                    zone.ValueRW.teamOwner = teamInt;

                    Entity evt = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(evt, new SupplyZoneCapturedEvent
                    {
                        zoneId = zone.ValueRO.zoneId,
                        capturingTeam = presentTeam
                    });
                }
            }

            alliedHeroes.Dispose();
        }
    }
}
