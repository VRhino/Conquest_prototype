using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stamina of a squad (Aprendizajes_Referencia.md L5-7, user decision 2026-11-03: one pool per squad). Running costs per
/// second for each soldier in the Run gear; walking and standing recover. Empty, the squad cannot run until it recovers
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
    public static void Step(ref SquadStaminaComponent s, int runners, float deltaTime)
    {
        if (s.max <= 0f) { s.exhausted = false; return; }
        if (runners > 0 && s.runCostPerSoldier > 0f) s.current -= runners * s.runCostPerSoldier * deltaTime;
        else s.current += s.recoveryPerSecond * deltaTime;
        s.current = math.clamp(s.current, 0f, s.max);
        if (s.current <= 0f) s.exhausted = true;
        else if (s.exhausted && s.current >= math.saturate(s.recoverFraction) * s.max) s.exhausted = false;
    }
}

/// <summary>
/// Spends and recovers each squad's stamina from the gear its soldiers ran in (L5-7). Reads the gears
/// <see cref="UnitFollowFormationSystem"/> chose this frame; only writes <see cref="SquadStaminaComponent"/>.
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
        var gears = SystemAPI.GetComponentLookup<UnitMovementGearComponent>(true);
        foreach (var (stamina, units) in SystemAPI.Query<RefRW<SquadStaminaComponent>, DynamicBuffer<SquadUnitElement>>())
        {
            int runners = 0;
            for (int i = 0; i < units.Length; i++)
                if (gears.TryGetComponent(units[i].Value, out var gear) && gear.gear == MovementGear.Run) runners++;
            SquadStamina.Step(ref stamina.ValueRW, runners, dt);
        }
    }
}
