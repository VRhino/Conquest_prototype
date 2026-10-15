using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

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
    private readonly List<Collider> _shooterColliders = new();

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
                    _shooterColliders.Clear();
                    if (VisualSyncUtility.TryGetVisual(World, r.shooter, out var shooterVisual))
                        shooterVisual.GetComponentsInChildren(true, _shooterColliders);
                    controller.Initialize(
                        r.shooter,
                        r.target,
                        r.spawnPosition,
                        r.attackDirection,
                        r.damageProfile,
                        r.damageProfileSnapshot,
                        r.sourceTeam,
                        r.multiplier,
                        r.poolKey.ToString(),
                        r.trajectory,
                        _shooterColliders);
                }
            }

            ecb.DestroyEntity(entity);
        }
    }
}
