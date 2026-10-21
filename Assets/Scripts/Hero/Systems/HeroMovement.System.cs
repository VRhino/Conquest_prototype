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
[UpdateAfter(typeof(HeroInputSystem))]
[UpdateAfter(typeof(HeroSpawnSystem))]
public partial class HeroMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        bool battleActive = !SystemAPI.TryGetSingleton<MatchStateComponent>(out var match)
            || match.currentState == MatchState.InBattle;
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

            float2 moveInput = input.MoveInput;
            var cam = Camera.main;
            bool awaitingSpawn = EntityManager.HasComponent<HeroSpawnComponent>(entity)
                && !EntityManager.GetComponentData<HeroSpawnComponent>(entity).hasSpawned;
            if (!battleActive || !life.isAlive || awaitingSpawn || cam == null)
            {
                // Never leave the previous frame's movement request live on an early exit.
                if (EntityManager.HasComponent<HeroMoveIntent>(entity))
                    EntityManager.SetComponentData(entity, default(HeroMoveIntent));
                continue;
            }

            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            float3 desired = (float3)(camForward * moveInput.y + camRight * moveInput.x);
            float3 direction = math.lengthsq(desired) > 0f ? math.normalize(desired) : float3.zero;
            // Walking speed from the stack (0 while immobilised by a shield-break stun); heroes off the stack walk at
            // their base speed.
            float currentSpeed = EntityManager.HasComponent<MoveSpeedComponent>(entity)
                ? EntityManager.GetComponentData<MoveSpeedComponent>(entity).value
                : stats.baseSpeed;
            if (currentSpeed <= 0f)
            {
                direction = float3.zero;
                currentSpeed = 0f;
            }
            else if (input.IsSprintPressed && !stamina.isExhausted && stamina.currentStamina > 0f)
            {
                currentSpeed *= stats.sprintMultiplier;
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
