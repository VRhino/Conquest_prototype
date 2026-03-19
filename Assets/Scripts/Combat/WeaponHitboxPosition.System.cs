using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Updates the LocalTransform of each WeaponHitboxEntity every frame so it
/// tracks the tip-center of its owner unit's weapon.
///
/// tipCenter = ownerPos + forward*(damageZoneStart + halfD) + up*yOffset
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct WeaponHitboxPositionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var weaponLookup    = SystemAPI.GetComponentLookup<UnitWeaponComponent>(true);

        foreach (var (hitboxTransform, owner) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<WeaponHitboxOwner>>())
        {
            Entity ownerEntity = owner.ValueRO.ownerUnit;
            if (!transformLookup.HasComponent(ownerEntity) ||
                !weaponLookup.HasComponent(ownerEntity))
                continue;

            var ownerTf = transformLookup[ownerEntity];
            var weapon  = weaponLookup[ownerEntity];

            float3 forward  = math.forward(ownerTf.Rotation);
            float  halfD    = math.max(0.01f, (weapon.attackRange - weapon.damageZoneStart) * 0.5f);
            float3 tipCenter = ownerTf.Position
                             + forward * (weapon.damageZoneStart + halfD)
                             + new float3(0f, weapon.damageZoneYOffset, 0f);

            hitboxTransform.ValueRW.Position = tipCenter;
            hitboxTransform.ValueRW.Rotation = ownerTf.Rotation;
        }
    }
}
