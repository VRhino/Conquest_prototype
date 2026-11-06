using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// A soldier's part of a squad ability run soldier by soldier (Aprendizajes_Referencia.md L2-17; ref.: soldiers launch
/// their own skills through a path of their own). Enabled when the squad gives the order, disabled when the soldier's run
/// ends or it gives up. Written only by <see cref="SkillExecutionSystem"/>.
/// </summary>
public struct SoldierSkillComponent : IComponentData, IEnableableComponent
{
    public BlobAssetReference<SkillBlob> skill;
    public int skillId;
    /// <summary>The squad that gave the order: its profile, team and effects are the hit's.</summary>
    public Entity squad;
    /// <summary>Id of the squad's launch, shared by all its soldiers.</summary>
    public uint orderId;
    /// <summary>When the soldier may start (the order plus its stagger), and when it gives up if still not free.</summary>
    public double startAt;
    public double giveUpAt;
    /// <summary>Its own run of the skill's phases once started.</summary>
    public SkillInstanceComponent instance;
}

/// <summary>How a squad order is spread over its soldiers (L2-17), in <see cref="SquadSpawnConfigComponent"/>.</summary>
[Serializable]
public struct SoldierOrderTuning
{
    [Tooltip("Cada soldado empieza en un momento distinto entre 0 y esto tras la orden (tirada sembrada), s.")]
    [Min(0f)] public float staggerWindow;
    [Tooltip("Lo más que espera un soldado ocupado (a mitad de un golpe o aturdido) antes de saltarse la orden, s.")]
    [Min(0f)] public float maxWait;

    public static SoldierOrderTuning Default => new() { staggerWindow = 0.6f, maxWait = 1.5f };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public SoldierOrderTuning OrDefault() => maxWait > 0f ? this : Default;
}

/// <summary>What a soldier with a pending order does this frame (L2-17).</summary>
public enum SoldierOrderStep : byte { Wait, Start, GiveUp }

/// <summary>Pure rules of a squad order run soldier by soldier (L2-17).</summary>
public static class SoldierOrders
{
    /// <summary>The soldier's start: the order time plus a share <paramref name="roll"/> (0-1) of the stagger window.</summary>
    public static double StartAt(double orderedAt, float roll, in SoldierOrderTuning tuning)
        => orderedAt + Mathf.Clamp01(roll) * tuning.staggerWindow;

    /// <summary>A pending soldier starts once its time came and it is free; past its give-up time it skips the order.</summary>
    public static SoldierOrderStep Step(double now, double startAt, double giveUpAt, bool free)
    {
        if (now < startAt) return SoldierOrderStep.Wait;
        if (free) return SoldierOrderStep.Start;
        return now >= giveUpAt ? SoldierOrderStep.GiveUp : SoldierOrderStep.Wait;
    }
}
