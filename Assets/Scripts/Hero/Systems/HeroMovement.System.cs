using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Handles deterministic movement and rotation of the local hero based on
/// <see cref="HeroInputComponent"/> and <see cref="HeroStatsComponent"/> data.
/// Applies sprint multiplier when sprinting and stamina is available.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var query = GetEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<HeroInputComponent>(),
            ComponentType.ReadOnly<HeroStatsComponent>(),
            ComponentType.ReadOnly<StaminaComponent>(),
            ComponentType.ReadOnly<HeroLifeComponent>());

        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var input = EntityManager.GetComponentData<HeroInputComponent>(entity);
            var stats = EntityManager.GetComponentData<HeroStatsComponent>(entity);
            var stamina = EntityManager.GetComponentData<StaminaComponent>(entity);
            var life = EntityManager.GetComponentData<HeroLifeComponent>(entity);

            if (!life.isAlive)
                continue;

            float2 moveInput = input.MoveInput;
            var cam = Camera.main;
            if (cam == null)
                continue;

            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            float3 desired = (float3)(camForward * moveInput.y + camRight * moveInput.x);
            float3 direction = math.lengthsq(desired) > 0f ? math.normalize(desired) : float3.zero;
            float currentSpeed = stats.baseSpeed;
            if (input.IsSprintPressed && !stamina.isExhausted && stamina.currentStamina > 0f)
            {
                currentSpeed *= stats.sprintMultiplier;
            }

            // Shield break stun: freeze hero movement
            if (EntityManager.HasComponent<UnitShieldComponent>(entity))
            {
                var heroShield = EntityManager.GetComponentData<UnitShieldComponent>(entity);
                if (heroShield.brokenTimer > 0f)
                {
                    direction = float3.zero;
                    currentSpeed = 0f;
                }
            }

            if (EntityManager.HasComponent<HeroMoveIntent>(entity))
            {
                EntityManager.SetComponentData(entity, new HeroMoveIntent { Direction = direction, Speed = currentSpeed });
            }
            else
            {
                EntityManager.AddComponentData(entity, new HeroMoveIntent { Direction = direction, Speed = currentSpeed });
            }
        }
    }
}
