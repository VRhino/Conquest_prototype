using Unity.Entities;
using Unity.Collections;

/// <summary>
/// Populates <see cref="SquadStatusComponent"/> on the local player's squad
/// so the battle HUD can display alive/total unit counts.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageCalculationSystem))]
public partial class SquadStatusUpdateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var heroSquadRef in SystemAPI.Query<RefRO<HeroSquadReference>>()
                     .WithAll<IsLocalPlayer>())
        {
            Entity squadEntity = heroSquadRef.ValueRO.squad;

            if (!SystemAPI.Exists(squadEntity))
                continue;

            bool hasStatus = SystemAPI.HasComponent<SquadStatusComponent>(squadEntity);

            // Ensure squad entity has IsLocalPlayer tag so the UI query finds it
            if (!SystemAPI.HasComponent<IsLocalPlayer>(squadEntity))
                ecb.AddComponent<IsLocalPlayer>(squadEntity);

            // Count alive/total units
            if (!SystemAPI.HasBuffer<SquadUnitElement>(squadEntity))
                continue;

            var units = SystemAPI.GetBuffer<SquadUnitElement>(squadEntity);
            int total = units.Length;
            int alive = 0;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (SystemAPI.Exists(unit) && !SystemAPI.HasComponent<IsDeadComponent>(unit))
                    alive++;
            }

            var status = new SquadStatusComponent
            {
                aliveUnits = alive,
                totalUnits = total
            };

            if (hasStatus)
                ecb.SetComponent(squadEntity, status);
            else
                ecb.AddComponent(squadEntity, status);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
