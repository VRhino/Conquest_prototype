using ConquestTactics.Animation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stamina of a squad (Aprendizajes_Referencia.md L5-7, user decision 2026-11-03: one pool per squad). Running costs per
/// second for each soldier that really runs (<see cref="SquadStamina.IsRunning"/>); otherwise the squad recovers. Empty, the squad cannot run until it recovers
/// <see cref="recoverFraction"/> of its maximum. A maximum of 0 = no stamina (as before). Written only by
/// <see cref="SquadStaminaSystem"/>; <see cref="UnitFollowFormationSystem"/> reads <see cref="exhausted"/>.
/// </summary>
public struct SquadStaminaComponent : IComponentData
{
    public float current;
    public float max;
    public float runCostPerSoldier;
    public float recoveryPerSecond;
    public float recoverFraction;
    public bool exhausted;

    public static SquadStaminaComponent From(in SquadDataComponent data) => new SquadStaminaComponent
    {
        current = data.staminaMax, max = data.staminaMax, runCostPerSoldier = data.staminaRunCostPerSoldier,
        recoveryPerSecond = data.staminaRecoveryPerSecond, recoverFraction = data.staminaRecoverFraction,
    };
}

/// <summary>Pure rule of the squad stamina (L5-7).</summary>
public static class SquadStamina
{
    /// <summary>
    /// Really running: in the Run gear and faster than <paramref name="factor"/> × its walking speed. A soldier with a
    /// combat target is in the Run gear even while it fights standing, and that must not tire the squad.
    /// </summary>
    public static bool IsRunning(MovementGear gear, float speed, float walkingSpeed, float factor)
        => gear == MovementGear.Run && speed > walkingSpeed * factor;

    /// <param name="costScale">The squad's <see cref="StatId.StaminaCost"/> (M18): 1 = the squad type's cost, 0 = running is free.</param>
    public static void Step(ref SquadStaminaComponent s, int runners, float deltaTime, float costScale = 1f)
    {
        if (s.max <= 0f) { s.exhausted = false; return; }
        // Running never recovers, even when it costs nothing.
        if (runners > 0) s.current -= runners * s.runCostPerSoldier * math.max(0f, costScale) * deltaTime;
        else s.current += s.recoveryPerSecond * deltaTime;
        s.current = math.clamp(s.current, 0f, s.max);
        if (s.current <= 0f) s.exhausted = true;
        else if (s.exhausted && s.current >= math.saturate(s.recoverFraction) * s.max) s.exhausted = false;
    }
}

/// <summary>
/// Spends and recovers each squad's stamina from its soldiers that really run (L5-7, <see cref="SquadStamina.IsRunning"/>).
/// Reads the gears <see cref="UnitFollowFormationSystem"/> chose this frame, the measured speeds and the squad's resolved
/// stamina cost (M18, <see cref="SquadAIStatsComponent.staminaCostScale"/>); only writes <see cref="SquadStaminaComponent"/>.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFollowFormationSystem))]
[BurstCompile]
public partial struct SquadStaminaSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        float factor = SystemAPI.TryGetSingleton<MovementTuningComponent>(out var tuning)
            ? tuning.staminaRunSpeedFactor : MovementTuningComponent.Default.staminaRunSpeedFactor;
        var gears = SystemAPI.GetComponentLookup<UnitMovementGearComponent>(true);
        var motion = SystemAPI.GetComponentLookup<UnitAnimationMovementComponent>(true);
        var squadStats = SystemAPI.GetComponentLookup<SquadAIStatsComponent>(true);
        foreach (var (stamina, units, squad) in SystemAPI.Query<RefRW<SquadStaminaComponent>, DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            int runners = 0;
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (gears.TryGetComponent(unit, out var gear) && motion.TryGetComponent(unit, out var m)
                    && SquadStamina.IsRunning(gear.gear, m.CurrentSpeed, m.MaxSpeed, factor)) runners++;
            }
            float costScale = squadStats.TryGetComponent(squad, out var stats) ? stats.staminaCostScale : 1f;
            SquadStamina.Step(ref stamina.ValueRW, runners, dt, costScale);
        }
    }
}
