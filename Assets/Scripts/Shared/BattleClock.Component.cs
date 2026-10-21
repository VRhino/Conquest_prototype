using Unity.Entities;

/// <summary>
/// Battle clock (Aprendizajes_Referencia.md L7-1): the remaining battle time lives in ECS, not in a MonoBehaviour.
/// Created by <see cref="BattleBootstrapSystem"/> when the battle starts, counted down by <see cref="BattleClockSystem"/>
/// and extended by <see cref="BattleTimerExtensionSystem"/>; the HUD only reads it.
/// </summary>
public struct BattleClockComponent : IComponentData
{
    public float remainingSeconds;
    /// <summary>Extensions never take the remaining time above this.</summary>
    public float maxSeconds;
    /// <summary>Seconds added for each captured zone.</summary>
    public float captureExtensionSeconds;
    /// <summary>Set once the clock reached zero.</summary>
    public bool expired;
}
