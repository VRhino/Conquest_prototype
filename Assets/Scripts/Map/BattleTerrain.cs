using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Terrain of a battle map (Aprendizajes_Referencia.md L1-14; ref.: one terrain per level, declared by its design row).
/// One byte, never reordered: append new terrains at the end.
/// </summary>
public enum BattleTerrain : byte
{
    /// <summary>On a map: none declared. On a modifier: applies on every terrain.</summary>
    Any,
    Plains,
    Forest,
    Mountain,
    Snow,
}

/// <summary>The terrain of the current battle, from its map (<c>MapDataSO</c>); written by <see cref="BattleBootstrapSystem"/>.</summary>
public struct BattleTerrainComponent : IComponentData
{
    public BattleTerrain value;
}

/// <summary>
/// A stat modifier a squad type declares for one terrain (L1-14): health, damage, defense or speed of its units, added
/// to the stack with source <see cref="ModifierSource.Terrain"/> when the battle's terrain matches.
/// </summary>
[Serializable]
public struct TerrainModifier
{
    [Tooltip("Terreno en el que aplica; Any = en todos.")]
    public BattleTerrain terrain;
    public StatId stat;
    public ModifierOp op;
    [Tooltip("Ratio (0,1 = +10 %), plano o valor que sustituye, según la operación.")]
    public float value;
}

/// <summary>A terrain modifier baked from the squad data; each unit gets the ones of the battle's terrain on spawn.</summary>
public struct SquadTerrainModifierElement : IBufferElementData
{
    public TerrainModifier value;
}

/// <summary>Pure rule of terrain modifiers (L1-14).</summary>
public static class TerrainModifiers
{
    /// <summary>A modifier counts on its own terrain or, if declared for <see cref="BattleTerrain.Any"/>, on every one.</summary>
    public static bool Applies(in TerrainModifier modifier, BattleTerrain battle)
        => modifier.terrain == BattleTerrain.Any || modifier.terrain == battle;

    /// <summary>Damage and penetration also live on the squad's shared damage profile, which the hits use.</summary>
    public static bool OnProfile(StatId stat) => IsDamage(stat) || IsPenetration(stat);

    /// <summary>
    /// Everything but penetration also goes on the unit: its own penetration adds to the profile's in the formula, so
    /// putting it on both would count it twice (damage on the unit is only shown, as the Level ratios do).
    /// </summary>
    public static bool OnUnit(StatId stat) => !IsPenetration(stat);

    static bool IsDamage(StatId stat) => stat == StatId.BluntDamage || stat == StatId.SlashDamage || stat == StatId.PierceDamage;

    static bool IsPenetration(StatId stat)
        => stat == StatId.BluntPenetration || stat == StatId.SlashPenetration || stat == StatId.PiercePenetration;
}
