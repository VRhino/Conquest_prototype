using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Line-of-sight settings (L4-03), in <see cref="SquadSpawnConfigComponent"/>.</summary>
[Serializable]
public struct LineOfSightTuning
{
    /// <summary>Default and Ground: the scene's walls, buildings and relief; units, heroes and projectiles are left out.</summary>
    public const int DefaultBlockingLayers = (1 << 0) | (1 << 3);

    [Tooltip("Sin línea de visión, una escuadra no detecta al enemigo. Apagado = solo distancia, como antes.")]
    public bool enabled;
    [Tooltip("Capas que tapan la vista (muros, edificios, relieve).")]
    public LayerMask blockingLayers;
    [Tooltip("Rayos por frame entre todas las escuadras, repartidos según las parejas de cada una.")]
    [Min(1)] public int raysPerFrame;
    [Tooltip("Altura de los ojos sobre el suelo, en origen y destino del rayo, m.")]
    [Min(0f)] public float eyeHeight;
    [Tooltip("Rayos recordados por pareja (1-8): la pareja deja de verse cuando todos están tapados.")]
    [Range(1, 8)] public int memory;

    public static LineOfSightTuning Default => new LineOfSightTuning
    {
        enabled = true, blockingLayers = DefaultBlockingLayers, raysPerFrame = 64, eyeHeight = 1.5f, memory = 4,
    };
}

/// <summary>Enemies within a squad's detection range, visible or not (squad or hero). Written by <see cref="EnemyDetectionSystem"/>.</summary>
public struct SquadInRangeEnemy : IBufferElementData
{
    public Entity Value;
}

/// <summary>
/// What a squad sees of one enemy in range (L4-03, ref.: a visibility bit per pair, all visible when the enemy is added,
/// set or cleared by batched rays). Written only by <see cref="LineOfSightSystem"/>.
/// </summary>
public struct SquadVisibilityElement : IBufferElementData
{
    /// <summary>An enemy squad or hero.</summary>
    public Entity enemy;
    /// <summary>Result of the last rays, newest in bit 0: 1 = clear. A new enemy starts all clear.</summary>
    public byte clearMask;
    /// <summary>Which of the enemy's soldiers the next ray aims at.</summary>
    public byte sampleCursor;
}

/// <summary>Round-robin position of a squad's next ray among its pairs.</summary>
public struct SquadVisibilityCursor : IComponentData
{
    public int next;
}

/// <summary>Pure rules of the line of sight (L4-03).</summary>
public static class LineOfSight
{
    public const byte AllClear = 0xFF;

    /// <summary>Seen while any of the remembered rays was clear.</summary>
    public static bool IsVisible(byte clearMask, int memory) => (clearMask & MemoryMask(memory)) != 0;

    public static byte Record(byte clearMask, bool clear) => (byte)((clearMask << 1) | (clear ? 1 : 0));

    static int MemoryMask(int memory) => (1 << math.clamp(memory, 1, 8)) - 1;

    /// <summary>Visible unless the squad has a pair for the enemy that the rays found blocked.</summary>
    public static bool Sees(DynamicBuffer<SquadVisibilityElement> visibility, Entity enemy, int memory)
    {
        for (int i = 0; i < visibility.Length; i++)
            if (visibility[i].enemy == enemy) return IsVisible(visibility[i].clearMask, memory);
        return true;
    }

    /// <summary>
    /// Hidden only when the squad has a pair for the enemy and its rays found it blocked; an enemy out of range has no
    /// pair and is not hidden (ambush, L1-15).
    /// </summary>
    public static bool Hides(DynamicBuffer<SquadVisibilityElement> visibility, Entity enemy, int memory)
    {
        for (int i = 0; i < visibility.Length; i++)
            if (visibility[i].enemy == enemy) return !IsVisible(visibility[i].clearMask, memory);
        return false;
    }

    /// <summary>
    /// Shares <paramref name="budget"/> rays among squads by demand, as the reference does: everyone gets its demand if it
    /// fits, else <c>demand · budget / total</c>, and what rounding leaves goes to the first ones that still want more.
    /// </summary>
    public static void Quotas(NativeArray<int> demands, int budget, NativeArray<int> quotas)
    {
        int total = 0;
        for (int i = 0; i < demands.Length; i++) total += math.max(0, demands[i]);
        if (total <= budget)
        {
            for (int i = 0; i < demands.Length; i++) quotas[i] = math.max(0, demands[i]);
            return;
        }
        int given = 0;
        for (int i = 0; i < demands.Length; i++)
        {
            quotas[i] = (int)((long)math.max(0, demands[i]) * budget / total);
            given += quotas[i];
        }
        for (int i = 0; i < demands.Length && given < budget; i++)
            if (quotas[i] < demands[i]) { quotas[i]++; given++; }
    }

    /// <summary>
    /// Matches a squad's pairs to the enemies now in range: those that stay keep their memory, new ones start all clear,
    /// gone ones are dropped.
    /// </summary>
    public static void Sync(DynamicBuffer<SquadVisibilityElement> visibility, DynamicBuffer<SquadInRangeEnemy> inRange)
    {
        for (int i = visibility.Length - 1; i >= 0; i--)
        {
            bool stays = false;
            for (int j = 0; j < inRange.Length && !stays; j++) stays = inRange[j].Value == visibility[i].enemy;
            if (!stays) visibility.RemoveAt(i);
        }
        for (int j = 0; j < inRange.Length; j++)
        {
            bool known = false;
            for (int i = 0; i < visibility.Length && !known; i++) known = visibility[i].enemy == inRange[j].Value;
            if (!known) visibility.Add(new SquadVisibilityElement { enemy = inRange[j].Value, clearMask = AllClear });
        }
    }
}
