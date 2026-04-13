using Unity.Entities;

/// <summary>
/// ECS-to-GameObject bridge: reads ProjectileSpawnRequest entities created by
/// RangedAttackSystem, retrieves a pooled GO from ObjectPoolSystem, hands it to
/// ProjectileController.Initialize(), then destroys the request entity.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RangedAttackSystem))]
public partial class ProjectileSpawnSystem : SystemBase
{
    EndSimulationEntityCommandBufferSystem _ecbSystem;

    protected override void OnCreate()
    {
        _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (ObjectPoolSystem.Instance == null)
            return;

        var ecb = _ecbSystem.CreateCommandBuffer();

        foreach (var (request, entity) in
                 SystemAPI.Query<RefRO<ProjectileSpawnRequest>>().WithEntityAccess())
        {
            var r  = request.ValueRO;
            var go = ObjectPoolSystem.Instance.GetFromPool(r.poolKey.ToString());

            if (go != null)
            {
                var controller = go.GetComponent<ProjectileController>();
                if (controller != null)
                {
                    controller.Initialize(
                        r.shooter,
                        r.target,
                        r.spawnPosition,
                        r.attackDirection,
                        r.damageProfile,
                        r.sourceTeam,
                        r.multiplier,
                        r.poolKey.ToString(),
                        r.trajectory);
                }
            }

            ecb.DestroyEntity(entity);
        }
    }
}
