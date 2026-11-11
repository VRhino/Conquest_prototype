using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Handles deterministic movement and rotation of the local hero based on
/// <see cref="HeroInputComponent"/> and <see cref="HeroStatsComponent"/> data.
/// Applies sprint multiplier when sprinting and stamina is available. On horseback (M19) the horse's gaits, turning
/// and stamina apply instead.
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

            // On horseback (M19) the horse moves: its gaits, its turning and its stamina.
            if (EntityManager.HasComponent<HeroMountComponent>(entity))
            {
                var mount = EntityManager.GetComponentData<HeroMountComponent>(entity);
                if (mount.Riding)
                {
                    WriteIntent(entity, Mounted(entity, mount, direction, input.IsSprintPressed, currentSpeed <= 0f));
                    continue;
                }
            }

            if (currentSpeed <= 0f)
            {
                direction = float3.zero;
                currentSpeed = 0f;
            }
            else if (input.IsSprintPressed && !stamina.isExhausted && stamina.currentStamina > 0f)
            {
                currentSpeed *= stats.sprintMultiplier;
            }

            // A skill dash takes over the movement while it lasts.
            if (EntityManager.HasComponent<HeroDashComponent>(entity))
            {
                var dash = EntityManager.GetComponentData<HeroDashComponent>(entity);
                if (dash.Active(SystemAPI.Time.ElapsedTime)) { direction = dash.direction; currentSpeed = dash.speed; }
            }

            WriteIntent(entity, new HeroMoveIntent { Direction = direction, Speed = currentSpeed });
        }
    }

    void WriteIntent(Entity entity, HeroMoveIntent intent)
    {
        if (EntityManager.HasComponent<HeroMoveIntent>(entity))
            EntityManager.SetComponentData(entity, intent);
        else
            EntityManager.AddComponentData(entity, intent);
    }

    /// <summary>
    /// The horse's step (M19): it only goes forward, turning towards the input at its turn rate, and speeds up or
    /// brakes towards the trot, or the gallop while Shift is held and it has stamina. Getting on or off, or stunned,
    /// it stands still. The motor's measured speed is the current one, so a wall stops it.
    /// </summary>
    HeroMoveIntent Mounted(Entity hero, in HeroMountComponent mount, float3 desired, bool gallopHeld, bool immobilised)
    {
        if (mount.state != HeroMountState.Mounted || immobilised) return default;
        var tuning = SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config) ? config.mount.OrDefault() : MountTuning.Default;
        float dt = SystemAPI.Time.DeltaTime;
        float3 forward = math.mul(EntityManager.GetComponentData<LocalTransform>(hero).Rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;
        forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
        float current = EntityManager.HasComponent<HeroMotorStateComponent>(hero)
            ? math.length(EntityManager.GetComponentData<HeroMotorStateComponent>(hero).velocity.xz) : 0f;
        bool hasInput = math.lengthsq(desired) > 0f;
        bool gallop = gallopHeld && EntityManager.Exists(mount.horse) && EntityManager.HasComponent<StaminaComponent>(mount.horse)
            && !EntityManager.GetComponentData<StaminaComponent>(mount.horse).isExhausted
            && EntityManager.GetComponentData<StaminaComponent>(mount.horse).currentStamina > 0f;
        float3 heading = hasInput ? HeroMountRules.Steer(forward, desired, tuning.turnRate, dt) : forward;
        float speed = HeroMountRules.Approach(current, HeroMountRules.TargetSpeed(tuning, hasInput, gallop, forward, desired), tuning, dt);
        return speed > 0.01f ? new HeroMoveIntent { Direction = heading, Speed = speed } : default;
    }
}
