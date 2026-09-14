using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class SquadProgressionCurves
{
    public const int MaxLevel = 30;

    // Missing configuration preserves base stats; it must not invent progression.
    public static BlobAssetReference<SquadProgressionCurveBlob> Create(SquadProgressionData data)
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<SquadProgressionCurveBlob>();
        var health = builder.Allocate(ref root.health, MaxLevel);
        var damage = builder.Allocate(ref root.damage, MaxLevel);
        var defense = builder.Allocate(ref root.defense, MaxLevel);
        var speed = builder.Allocate(ref root.speed, MaxLevel);
        for (int i = 0; i < MaxLevel; i++)
        {
            health[i] = Sample(data != null ? data.healthCurve : null, i + 1);
            damage[i] = Sample(data != null ? data.damageCurve : null, i + 1);
            defense[i] = Sample(data != null ? data.defenseCurve : null, i + 1);
            speed[i] = Sample(data != null ? data.speedCurve : null, i + 1);
        }
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
}
