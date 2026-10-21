using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class SquadProgressionCurves
{
    public const int MaxLevel = 30;

    /// <summary>XP from level 1 to 2 when a squad has no table (the curve the game always used).</summary>
    public const float DefaultBaseXP = 100f;
    /// <summary>Growth of the XP needed per level when a squad has no table.</summary>
    public const float DefaultXPGrowth = 1.1f;
    /// <summary>Levels between ability unlocks when a squad has no unlock table.</summary>
    public const int DefaultUnlockInterval = 10;

    // Missing configuration preserves base stats; it must not invent progression.
    public static BlobAssetReference<SquadProgressionCurveBlob> Create(SquadProgressionData data)
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<SquadProgressionCurveBlob>();
        var health = builder.Allocate(ref root.health, MaxLevel);
        var damage = builder.Allocate(ref root.damage, MaxLevel);
        var defense = builder.Allocate(ref root.defense, MaxLevel);
        var speed = builder.Allocate(ref root.speed, MaxLevel);
        var xp = builder.Allocate(ref root.xpToNextLevel, MaxLevel);
        for (int i = 0; i < MaxLevel; i++)
        {
            health[i] = Sample(data != null ? data.healthCurve : null, i + 1);
            damage[i] = Sample(data != null ? data.damageCurve : null, i + 1);
            defense[i] = Sample(data != null ? data.defenseCurve : null, i + 1);
            speed[i] = Sample(data != null ? data.speedCurve : null, i + 1);
            float[] table = data != null ? data.xpToNextLevel : null;
            xp[i] = table != null && i < table.Length && table[i] > 0f ? table[i] : DefaultXPToNext(i + 1);
        }
        int[] unlocks = data != null && data.abilityUnlockLevels != null ? data.abilityUnlockLevels : new int[0];
        var unlock = builder.Allocate(ref root.abilityUnlockLevel, unlocks.Length);
        for (int i = 0; i < unlocks.Length; i++) unlock[i] = unlocks[i] > 0 ? unlocks[i] : DefaultUnlockLevel(i);
        return builder.CreateBlobAssetReference<SquadProgressionCurveBlob>(Allocator.Persistent);
    }

    static float Sample(AnimationCurve curve, int level)
    {
        float value = curve != null && curve.length > 0 ? curve.Evaluate(level) : 1f;
        return math.isfinite(value) && value > 0f ? value : 1f;
    }

    public static float Read(ref BlobArray<float> values, int level)
    {
        if (values.Length == 0) return 1f;
        float value = values[math.clamp(level - 1, 0, values.Length - 1)];
        return math.isfinite(value) && value > 0f ? value : 1f;
    }

    /// <summary>XP to go from <paramref name="level"/> to the next one, from the squad's table.</summary>
    public static float XPToNext(BlobAssetReference<SquadProgressionCurveBlob> curves, int level)
    {
        if (!curves.IsCreated) return DefaultXPToNext(level);
        ref var table = ref curves.Value.xpToNextLevel;
        if (table.Length == 0) return DefaultXPToNext(level);
        return table[math.clamp(level - 1, 0, table.Length - 1)];
    }

    /// <summary>Same as the blob read, from the authoring data (UI outside ECS).</summary>
    public static float XPToNext(SquadProgressionData data, int level)
    {
        float[] table = data != null ? data.xpToNextLevel : null;
        int i = level - 1;
        return table != null && i >= 0 && i < table.Length && table[i] > 0f ? table[i] : DefaultXPToNext(level);
    }

    /// <summary>Level the ability at <paramref name="index"/> (in the squad's order) unlocks at.</summary>
    public static int UnlockLevel(BlobAssetReference<SquadProgressionCurveBlob> curves, int index)
    {
        if (curves.IsCreated && index < curves.Value.abilityUnlockLevel.Length)
            return curves.Value.abilityUnlockLevel[index];
        return DefaultUnlockLevel(index);
    }

    public static float DefaultXPToNext(int level) => math.floor(DefaultBaseXP * math.pow(DefaultXPGrowth, level - 1));

    public static int DefaultUnlockLevel(int index) => (index + 1) * DefaultUnlockInterval;
}
