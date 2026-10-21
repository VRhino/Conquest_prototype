using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Singleton component written every frame by UIBattleDataSystem.
/// HUDController and other UI MonoBehaviours read from this component
/// instead of querying gameplay ECS components directly.
/// </summary>
public struct UIHeroBattleDataComponent : IComponentData
{
    // ── Hero vitals ──────────────────────────────────────────────────────────
    public float currentHealth;
    public float maxHealth;
    public float currentStamina;
    public float maxStamina;
    // ── Skill cooldowns (L2-5) ───────────────────────────────────────────────
    /// <summary>Fraction still to wait for Q, E, R and the ultimate (x, y, z, w); 0 = ready.</summary>
    public float4 skillCooldown;

    // ── Capture zone ─────────────────────────────────────────────────────────
    public bool  isInCaptureZone;
    public float captureProgress;   // 0–100

    // ── Squad change (one-frame pulse) ────────────────────────────────────────
    /// <summary>True only on the frame when a SquadChangeEvent was consumed.</summary>
    public bool squadChangedThisFrame;
    public int  newSquadId;

    // ── Squad status ─────────────────────────────────────────────────────────
    public int aliveUnits;
    public int totalUnits;
}
