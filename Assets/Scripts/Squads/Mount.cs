using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// A mounted soldier (M17, Aprendizajes_Referencia.md L5-8). In the troops the soldier and its horse are one body, so
/// there is no rider-to-mount binding (L5-6 comes with the mounted hero). Added at spawn to cavalry soldiers by
/// <see cref="SquadUnitSpawnBuilder"/>; read-only afterwards.
/// </summary>
public struct MountComponent : IComponentData
{
    /// <summary>Cosine of half the arc around the horse's front where the rider can strike or shoot; −1 = any direction.</summary>
    public float attackArcCos;
    /// <summary>Turn rate of the horse in combat, deg/s; 0 = the tuning's <c>combatTurnRate</c> (a soldier on foot).</summary>
    public float turnRate;
}

/// <summary>Pure rules of mounts (L5-8; ref.: a turn arc relative to the mount outside which the rider does not attack).</summary>
public static class MountRules
{
    /// <summary>Height a projectile leaves from on foot, m (the archers' shot before M17).</summary>
    public const float FootLaunchHeight = 1.2f;

    /// <summary>Cosine of half of a full arc in degrees; 0 or 360 and more = no limit (−1).</summary>
    public static float ArcCos(float arcDegrees)
        => arcDegrees <= 0f || arcDegrees >= 360f ? -1f : math.cos(math.radians(arcDegrees * 0.5f));

    /// <summary>Whether <paramref name="target"/> lies inside the arc around <paramref name="forward"/> seen from <paramref name="position"/> (on the ground plane).</summary>
    public static bool InArc(float3 forward, float3 position, float3 target, float arcCos)
    {
        if (arcCos <= -1f) return true;
        float2 to = target.xz - position.xz;
        float2 front = forward.xz;
        if (math.lengthsq(to) < 1e-6f || math.lengthsq(front) < 1e-6f) return true;
        return math.dot(math.normalize(front), math.normalize(to)) >= arcCos - 1e-5f;
    }

    /// <summary>Combat turn rate of a unit: its horse's, or the tuning's.</summary>
    public static float TurnRate(float mountTurnRate, float fallback) => mountTurnRate > 0f ? mountTurnRate : fallback;
}

/// <summary>
/// Gaits of a horse on screen (M17): which of its four in-place clips (Idle, Walk, Trot, Gallop) and how fast. The
/// rider's clips last as long as the horse's, so both Animators get the same two values and stay in step.
/// </summary>
public static class MountGait
{
    /// <summary>Ground speed of each clip, m/s (Assets/Art/Mounts/Horse/README.md): walk, trot and gallop.</summary>
    public const float WalkClipSpeed = 1.1f, TrotClipSpeed = 2.9f, GallopClipSpeed = 6.3f;
    /// <summary>Below this fraction of its march the horse walks; at its march it trots; at its run it gallops.</summary>
    public const float WalkFraction = 0.35f;

    /// <summary>
    /// Blend value of the gait tree: 0 idle, 1 walk, 2 trot, 3 gallop, linear between them. <paramref name="walkSpeed"/>
    /// is the squad's march and <paramref name="runSpeed"/> its run.
    /// </summary>
    public static float Blend(float speed, float walkSpeed, float runSpeed)
    {
        if (speed <= 0.05f || walkSpeed <= 0f) return 0f;
        float slow = WalkFraction * walkSpeed;
        if (speed <= slow) return speed / slow;
        if (speed <= walkSpeed) return 1f + (speed - slow) / (walkSpeed - slow);
        float run = math.max(runSpeed, walkSpeed * 1.01f);
        return math.min(3f, 2f + (speed - walkSpeed) / (run - walkSpeed));
    }

    /// <summary>Ground speed the blended clips cover at <paramref name="blend"/>, m/s.</summary>
    public static float ClipSpeed(float blend)
    {
        if (blend <= 1f) return WalkClipSpeed * math.max(0f, blend);
        if (blend <= 2f) return math.lerp(WalkClipSpeed, TrotClipSpeed, blend - 1f);
        return math.lerp(TrotClipSpeed, GallopClipSpeed, math.min(1f, blend - 2f));
    }

    /// <summary>Playback speed of the gait tree so the hooves do not slide: real speed over the clips' speed.</summary>
    public static float Scale(float speed, float blend)
    {
        float clip = ClipSpeed(blend);
        return clip < 0.2f ? 1f : math.clamp(speed / clip, 0.5f, 3f);
    }
}
