using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>Where the hero is in getting on and off its horse (M19).</summary>
public enum HeroMountState : byte
{
    OnFoot,
    /// <summary>Getting on (Hero_Mount): the horse is there, the hero does not move.</summary>
    Mounting,
    Mounted,
    /// <summary>Getting off (Hero_Dismount): the hero does not move.</summary>
    Dismounting,
}

/// <summary>Why the last ride ended (the visuals play a fall for the forced ones).</summary>
public enum HeroMountEnd : byte
{
    None,
    /// <summary>The hero got off (G).</summary>
    Dismounted,
    /// <summary>A hit while getting on.</summary>
    Interrupted,
    /// <summary>The horse died: the hero falls and goes on on foot.</summary>
    HorseKilled,
    /// <summary>A knock-down or a launch threw the hero off.</summary>
    ThrownOff,
    HeroDied,
}

/// <summary>
/// The hero's mount (M19). Written only by <see cref="HeroMountSystem"/>; the visuals follow <see cref="revision"/>.
/// The horse is a body of its own (<see cref="HeroHorseComponent"/>): its own health and stamina. While the hero
/// rides, its position is the horse's (the ground under the saddle).
/// </summary>
public struct HeroMountComponent : IComponentData
{
    public HeroMountState state;
    /// <summary>Seconds in the current state (getting on or off).</summary>
    public float elapsed;
    /// <summary>The horse, from the start of the mount to the end of the ride; Entity.Null on foot.</summary>
    public Entity horse;
    /// <summary>Bumped on every change of state.</summary>
    public uint revision;
    public HeroMountEnd lastEnd;
    /// <summary>The hero's health when it began to get on: losing any interrupts it.</summary>
    public float healthAtStart;
    /// <summary>The hero's last reaction already looked at (<see cref="ResilienceComponent.reactionRevision"/>).</summary>
    public uint reactionRevisionSeen;
    /// <summary>
    /// Where the hero's body jumps to when the ride starts or ends (the horse's centre, or the ground beside it). The
    /// motor moves the body when <see cref="poseJumpRevision"/> changes; the clips play the move in between.
    /// </summary>
    public float3 poseJumpPosition;
    public quaternion poseJumpRotation;
    public uint poseJumpRevision;
    /// <summary>Simulation time from which the horse can be called again (longer after it died).</summary>
    public double nextMountTime;

    public bool Riding => state != HeroMountState.OnFoot;
}

/// <summary>
/// An AI hero's wish to be on horseback (M19). Written only by <see cref="HeroAIMountSystem"/>; the player uses G
/// instead. <see cref="HeroMountSystem"/> calls the horse or gets off to match it.
/// </summary>
public struct HeroMountRequestComponent : IComponentData
{
    public bool ride;
}

/// <summary>A hero's horse (M19): a body of its own, with its own health and stamina, that follows its rider.</summary>
public struct HeroHorseComponent : IComponentData
{
    public Entity rider;
}

/// <summary>The hero's horse (M19), in <see cref="HeroGameplayConfigComponent"/>. Provisional values, to be balanced.</summary>
[Serializable]
public struct MountTuning
{
    [Tooltip("Vida del caballo del héroe.")]
    [Min(1f)] public float horseHealth;
    [Tooltip("Velocidad al trote (sin Shift), m/s.")]
    [Min(0f)] public float trotSpeed;
    [Tooltip("Velocidad al galope (con Shift), m/s.")]
    [Min(0f)] public float gallopSpeed;
    [Tooltip("Giro máximo del caballo, grados por segundo.")]
    [Min(1f)] public float turnRate;
    [Tooltip("Aceleración y frenada, m/s².")]
    [Min(0.1f)] public float acceleration;
    [Min(0.1f)] public float deceleration;
    [Tooltip("Aguante del caballo: máximo, gasto por segundo de galope y recuperación por segundo.")]
    [Min(1f)] public float stamina;
    [Min(0f)] public float gallopStaminaPerSecond;
    [Min(0f)] public float staminaRegenPerSecond;
    [Tooltip("Duración de Hero_Mount y Hero_Dismount, s.")]
    [Min(0.1f)] public float mountSeconds;
    [Min(0.1f)] public float dismountSeconds;
    [Tooltip("Altura sobre el suelo por encima de la cual un golpe da al jinete; por debajo, al caballo.")]
    [Min(0f)] public float riderHitHeight;
    [Tooltip("Segundos hasta poder llamar a otro caballo después de que muera el anterior.")]
    [Min(0f)] public float recallSeconds;
    [Tooltip("Segundos hasta poder volver a llamar al caballo tras bajar, caer o ser interrumpido.")]
    [Min(0f)] public float remountSeconds;

    public static MountTuning Default => new()
    {
        horseHealth = 1200f, trotSpeed = 6f, gallopSpeed = 11f, turnRate = 120f, acceleration = 6f, deceleration = 10f,
        stamina = 100f, gallopStaminaPerSecond = 8f, staminaRegenPerSecond = 10f, mountSeconds = 1.2f, dismountSeconds = 0.9f,
        riderHitHeight = 1.45f, recallSeconds = 20f, remountSeconds = 1.5f,
    };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public MountTuning OrDefault() => horseHealth > 0f && trotSpeed > 0f ? this : Default;
}

/// <summary>Pure rules of the hero's mount (M19).</summary>
public static class HeroMountRules
{
    /// <summary>
    /// The hero's feet beside the horse, in the horse's space (+Z forward, +X right): where Hero_Mount starts and
    /// Hero_Dismount ends (blender-tools/conquest-horse/scripts/anim_mount.py, GROUND_SPOT). On its left.
    /// </summary>
    public static readonly float3 GroundSpot = new(-0.60f, 0f, 0.0475f);

    /// <summary>Where the horse stands for a hero at <paramref name="feet"/> facing <paramref name="facing"/> to get on.</summary>
    public static float3 HorseFor(float3 feet, quaternion facing) => feet - math.mul(facing, GroundSpot);

    /// <summary>The ground beside a horse, where its rider gets off.</summary>
    public static float3 GroundBeside(float3 horse, quaternion facing) => horse + math.mul(facing, GroundSpot);

    /// <summary>Who can call the horse: alive, in battle, free to move and not attacking.</summary>
    public static bool CanMount(bool alive, bool inBattle, bool immobilised, bool busy) => alive && inBattle && !immobilised && !busy;

    /// <summary>The horse's heading after turning towards <paramref name="desired"/> at most <paramref name="turnRateDegrees"/>·dt.</summary>
    public static float3 Steer(float3 forward, float3 desired, float turnRateDegrees, float dt)
    {
        forward.y = desired.y = 0f;
        forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
        desired = math.normalizesafe(desired, forward);
        float angle = math.atan2(forward.x * desired.z - forward.z * desired.x, math.dot(forward, desired));   // + = to the left
        float step = math.radians(turnRateDegrees) * math.max(dt, 0f);
        float turn = math.clamp(angle, -step, step);
        return math.normalize(math.mul(quaternion.RotateY(-turn), forward));
    }

    /// <summary>
    /// Ground speed the rider asks for: none without input; the trot, or the gallop while Shift is held and the horse
    /// has stamina; a horse turning more than 90° slows to half.
    /// </summary>
    public static float TargetSpeed(in MountTuning tuning, bool hasInput, bool gallop, float3 forward, float3 desired)
    {
        if (!hasInput) return 0f;
        float speed = gallop ? tuning.gallopSpeed : tuning.trotSpeed;
        forward.y = desired.y = 0f;
        return math.dot(math.normalizesafe(forward), math.normalizesafe(desired)) < 0f ? speed * 0.5f : speed;
    }

    /// <summary>The horse's speed after accelerating or braking towards <paramref name="target"/>.</summary>
    public static float Approach(float current, float target, in MountTuning tuning, float dt)
    {
        float rate = target > current ? tuning.acceleration : tuning.deceleration;
        return current + math.clamp(target - current, -rate * dt, rate * dt);
    }

    /// <summary>A hit below the saddle on a riding hero lands on the horse; above it, on the rider.</summary>
    public static bool HitsHorse(float contactHeight, float riderGround, in MountTuning tuning)
        => contactHeight - riderGround < tuning.riderHitHeight;

    /// <summary>An AI hero rides to cover more than this, m; on horseback it keeps riding until this close, m.</summary>
    public const float AIRideDistance = 35f, AIKeepRidingDistance = 12f;
    /// <summary>An AI hero gets off (or does not get on) with an enemy hero this near, m: it fights on foot, with its skills.</summary>
    public const float AIFightOnFootDistance = 25f;

    /// <summary>Whether an AI hero wants to be on horseback: for a long way with no enemy near.</summary>
    public static bool AIWantsToRide(bool riding, float destinationDistance, bool underAttack, float enemyDistance)
    {
        if (underAttack || enemyDistance < AIFightOnFootDistance) return false;
        return destinationDistance > (riding ? AIKeepRidingDistance : AIRideDistance);
    }

    /// <summary>When the horse can be called again after a ride ended: much later if it died.</summary>
    public static double NextMountTime(double now, HeroMountEnd why, in MountTuning tuning)
        => now + (why == HeroMountEnd.HorseKilled ? tuning.recallSeconds : why == HeroMountEnd.HeroDied ? 0f : tuning.remountSeconds);

    /// <summary>A reaction that throws a rider off its horse.</summary>
    public static bool ThrowsOff(HitReaction reaction) => reaction is HitReaction.Knockdown or HitReaction.Launch;

    /// <summary>
    /// Who a hit that touched <paramref name="target"/> at <paramref name="contactPoint"/> lands on: below the saddle of
    /// a riding hero, its horse; otherwise the target itself. The hero's body (its collider) covers horse and rider.
    /// </summary>
    public static Entity HitTarget(EntityManager em, Entity target, float3 contactPoint, float riderHitHeight)
    {
        if (target == Entity.Null || !em.Exists(target) || !em.HasComponent<HeroMountComponent>(target)) return target;
        var mount = em.GetComponentData<HeroMountComponent>(target);
        if (!mount.Riding || mount.horse == Entity.Null || !em.Exists(mount.horse) || !em.HasComponent<LocalTransform>(target)) return target;
        float ground = em.GetComponentData<LocalTransform>(target).Position.y;
        return contactPoint.y - ground < riderHitHeight ? mount.horse : target;
    }

    /// <summary><see cref="HitTarget(EntityManager, Entity, float3, float)"/> with the configured height (for the visuals' side).</summary>
    public static Entity HitTarget(EntityManager em, Entity target, float3 contactPoint)
    {
        if (target == Entity.Null || !em.Exists(target) || !em.HasComponent<HeroMountComponent>(target)) return target;
        using var config = em.CreateEntityQuery(ComponentType.ReadOnly<HeroGameplayConfigComponent>());
        var tuning = config.TryGetSingleton<HeroGameplayConfigComponent>(out var c) ? c.mount.OrDefault() : MountTuning.Default;
        return HitTarget(em, target, contactPoint, tuning.riderHitHeight);
    }
}
