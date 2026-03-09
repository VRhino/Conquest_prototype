using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles capture logic for zones of type <see cref="ZoneType.Capture"/>.
/// Progress advances when only attackers are in range.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class CaptureZoneTriggerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);


        var linkLookup = GetComponentLookup<ZoneLinkComponent>(true);
        var zoneLookup = GetComponentLookup<ZoneTriggerComponent>();

        foreach (var (zone, progress, transform, entity) in
                 SystemAPI.Query<RefRW<ZoneTriggerComponent>,
                                   RefRW<CapturePointProgressComponent>,
                                   RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            if (zone.ValueRO.zoneType != ZoneType.Capture || !zone.ValueRO.isActive)
                continue;

            if (zone.ValueRO.isLocked || progress.ValueRO.captureProgress >= 100f)
                continue;

            bool defenders = false;
            bool attackers = false;
            float radiusSq = zone.ValueRO.radius * zone.ValueRO.radius;
            Team owner = (Team)zone.ValueRO.teamOwner;

            foreach (var (hTransform, life, team) in
                     SystemAPI.Query<RefRO<LocalTransform>,
                                     RefRO<HeroLifeComponent>,
                                     RefRO<TeamComponent>>())
            {
                if (!life.ValueRO.isAlive)
                    continue;

                if (math.distancesq(hTransform.ValueRO.Position, transform.ValueRO.Position) > radiusSq)
                    continue;

                if (team.ValueRO.value == owner)
                    defenders = true;
                else
                {
                    attackers = true;
                    progress.ValueRW.capturingTeam = (int)team.ValueRO.value;
                }
            }

            progress.ValueRW.isBeingCaptured = attackers;

            if (defenders)
            {
                progress.ValueRW.isContested = attackers; // contested solo si ambos presentes

                if (!attackers && progress.ValueRO.captureProgress > 0f)
                {
                    // Solo defensores: el progreso decrementa
                    progress.ValueRW.captureProgress = math.max(0f,
                        progress.ValueRO.captureProgress - progress.ValueRO.captureSpeed * dt);
                }
                continue;
            }

            progress.ValueRW.isContested = false;

            if (attackers)
            {
                // Solo atacantes: la captura avanza desde donde quedó
                progress.ValueRW.captureProgress = math.min(100f,
                    progress.ValueRO.captureProgress + progress.ValueRO.captureSpeed * dt);
            }

            if (progress.ValueRO.captureProgress >= 100f)
            {
                zone.ValueRW.teamOwner = progress.ValueRO.capturingTeam;
                zone.ValueRW.isActive = false;
                progress.ValueRW.isBeingCaptured = false;

                Entity evt = ecb.CreateEntity();
                ecb.AddComponent(evt, new ZoneCapturedEvent
                {
                    zoneId = zone.ValueRO.zoneId,
                    capturingTeam = (Team)progress.ValueRO.capturingTeam
                });

                foreach (var (targetZone, link, targetEntity) in
                         SystemAPI.Query<RefRW<ZoneTriggerComponent>, RefRO<ZoneLinkComponent>>()
                                .WithEntityAccess())
                {
                    if (link.ValueRO.requiredZoneId == zone.ValueRO.zoneId)
                    {
                        var z = targetZone.ValueRW;
                        z.isLocked = false;
                        targetZone.ValueRW = z;
                    }
                }

                if (zone.ValueRO.isFinal &&
                    SystemAPI.TryGetSingletonEntity<GameStateComponent>(out var stateEntity))
                {
                    var state = SystemAPI.GetComponentRW<GameStateComponent>(stateEntity);
                    state.ValueRW.currentPhase = GamePhase.PostPartida;

                    if (SystemAPI.TryGetSingletonEntity<MatchStateComponent>(out var matchEntity))
                    {
                        var match = SystemAPI.GetComponentRW<MatchStateComponent>(matchEntity);
                        match.ValueRW.victoryConditionMet = true;
                        match.ValueRW.winnerTeam = progress.ValueRO.capturingTeam;
                    }
                }
            }
        }
    }
}
