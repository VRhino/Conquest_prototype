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
        float deltaTime = SystemAPI.Time.DeltaTime;
        Entities
            .WithAll<IsLocalPlayer>()
            .WithStructuralChanges()
            .ForEach((Entity entity, ref HeroInputComponent input, in HeroStatsComponent stats, in StaminaComponent stamina, in HeroLifeComponent life) =>
            {
                if (!life.isAlive)
                    return;

                float2 moveInput = input.MoveInput;
                var cam = Camera.main;
                if (cam == null)
                    return;

                Vector3 camForward = cam.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                Vector3 camRight = cam.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                float3 desired = (float3)(camForward * moveInput.y + camRight * moveInput.x);
                float magnitude = math.length(desired);
                float3 direction = magnitude > 0f ? desired / magnitude : float3.zero;
                float currentSpeed = stats.baseSpeed;
                if (input.IsSprintPressed && !stamina.isExhausted && stamina.currentStamina > 0f)
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
            }).Run();
    }
}
