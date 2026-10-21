using Unity.Entities;

/// <summary>Values of <see cref="MatchStateComponent.winnerTeam"/>.</summary>
public static class MatchWinner
{
    public const int None = 0;
    public const int Attackers = 1;
    public const int Defenders = 2;
}

/// <summary>Event: the final zone was captured (emitted by <see cref="CaptureZoneTriggerSystem"/>).</summary>
public struct FinalZoneCapturedEvent : IComponentData
{
    /// <summary>Winner value of the team that captured it (1 attackers, 2 defenders).</summary>
    public int capturingTeam;
}

/// <summary>Event: the battle clock ran out (emitted by <see cref="BattleClockSystem"/>).</summary>
public struct BattleTimeExpiredEvent : IComponentData { }
