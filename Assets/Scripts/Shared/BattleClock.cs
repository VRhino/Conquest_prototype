using Unity.Mathematics;

/// <summary>Pure rules of the battle clock (L7-1); the defaults match <c>BattleData</c> and <c>MapDataSO</c>.</summary>
public static class BattleClock
{
    /// <summary>Battle length when neither the battle nor its map sets one (BattleData's default).</summary>
    public const int DefaultBattleSeconds = 900;
    public const int DefaultCaptureExtensionSeconds = 300;
    public const int DefaultMaxBattleSeconds = 1800;

    /// <summary>Starting time: the battle's own timer, else the map's duration, else the default.</summary>
    public static int InitialSeconds(int battleTimer, int mapDuration)
        => battleTimer > 0 ? battleTimer : mapDuration > 0 ? mapDuration : DefaultBattleSeconds;

    /// <summary>Remaining time after a capture: + extension, never above the cap (and never lowered by it).</summary>
    public static float Extend(float remaining, float extension, float max)
    {
        if (extension <= 0f) return remaining;
        return math.max(remaining, math.min(remaining + extension, max));
    }

    /// <summary>Remaining time after <paramref name="deltaTime"/>, never negative.</summary>
    public static float Tick(float remaining, float deltaTime) => math.max(0f, remaining - math.max(0f, deltaTime));

    /// <summary>mm:ss, rounding up so the display shows 00:00 only at the very end.</summary>
    public static string Format(float remaining)
    {
        int seconds = (int)math.ceil(math.max(0f, remaining));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
