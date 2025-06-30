using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

/// <summary>
/// Handles physical movement and orientation of the local hero based on
/// <see cref="HeroInputComponent"/> data.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (input, stats, life, velocity, mass, transform, entity) in
                 SystemAPI.Query<RefRO<HeroInputComponent>,
                                 RefRO<HeroStatsComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRW<PhysicsVelocity>,
                                 RefRO<PhysicsMass>,
                                 RefRW<LocalTransform>>()
                        .WithAll<IsLocalPlayer>()
                        .WithEntityAccess())
        {
            // Debug: Identifica la entidad y sus componentes clave
            // UnityEngine.Debug.Log($"[HeroMovementSystem] Entity: {entity}, isAlive: {life.ValueRO.isAlive}, baseSpeed: {stats.ValueRO.baseSpeed}, sprintMult: {stats.ValueRO.sprintMultiplier}, jumpForce: {stats.ValueRO.jumpForce}");
            // UnityEngine.Debug.Log($"[HeroMovementSystem] Input: moveInput={input.ValueRO.moveInput}, sprint={input.ValueRO.isSprinting}, jump={input.ValueRO.isJumping}");
            // UnityEngine.Debug.Log($"[HeroMovementSystem] PhysicsVelocity antes: {velocity.ValueRW.Linear}");

            if (!life.ValueRO.isAlive)
                continue;

            // --- Movimiento tipo third person, sin rebotes ni rotaciones físicas ---
            // Calcula la dirección de movimiento en el plano XZ
            float3 move = new float3(input.ValueRO.moveInput.x, 0f, input.ValueRO.moveInput.y);
            float moveMagnitude = math.length(move);
            float3 moveDir = moveMagnitude > 0f ? math.normalize(move) : float3.zero;

            // Aplica velocidad solo en el plano XZ
            float speed = stats.ValueRO.baseSpeed;
            if (input.ValueRO.isSprinting)
                speed *= stats.ValueRO.sprintMultiplier;

            float3 targetVel = moveDir * speed;
            var vel = velocity.ValueRW;

            // Suaviza el cambio de velocidad (aceleración/frenado)
            float acceleration = 40f; // Más alto = más responsivo
            vel.Linear.x = math.lerp(vel.Linear.x, targetVel.x, acceleration * deltaTime);
            vel.Linear.z = math.lerp(vel.Linear.z, targetVel.z, acceleration * deltaTime);

            // Mantén la velocidad vertical (gravedad DOTS Physics)
            // Salto
            if (input.ValueRO.isJumping)
            {
                if (SystemAPI.HasComponent<HeroGroundState>(entity) &&
                    SystemAPI.GetComponent<HeroGroundState>(entity).isGrounded)
                {
                    vel.Linear.y = stats.ValueRO.jumpForce;
                    var ground = SystemAPI.GetComponentRW<HeroGroundState>(entity);
                    ground.ValueRW.isGrounded = false;
                }
            }

            velocity.ValueRW = vel;

            // --- Rotación: solo gira hacia la dirección de movimiento si hay input ---
            if (moveMagnitude > 0.01f)
            {
                quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, targetRot, deltaTime * 10f);
            }
            else
            {
                // Mantén la rotación solo en Y si no hay input
                float3 forward = math.forward(transform.ValueRW.Rotation);
                float yAngle = math.atan2(forward.x, forward.z);
                transform.ValueRW.Rotation = quaternion.RotateY(yAngle);
            }
        }
    }
}
