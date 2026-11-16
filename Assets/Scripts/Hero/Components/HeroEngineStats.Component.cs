using System;
using Unity.Entities;

/// <summary>
/// A hero's numbers from the engine ticket (its class of <c>CLASES_HEROE</c> and its pieces of <c>OBJETOS</c>; forged pieces with their own
/// numbers): what <see cref="HeroInitializationSystem"/> puts on the stack in place of the class asset's attributes and the equipment's.
/// Filled by <c>ConquestTactics.Net.CifrasDelHeroe</c>; how they turn into combat stats is Conquest's (<see cref="HeroEngineRules"/>).
/// </summary>
[Serializable]
public struct HeroEngineStatsComponent : IComponentData
{
    /// <summary>The hero came from the ticket; false on a hero built from the class asset (single player, old saves).</summary>
    public bool fromEngine;
    /// <summary><c>atributos_efectivos</c>: the class base plus the points spent, without the equipment.</summary>
    public float strength, dexterity, armor, vitality;
    /// <summary>What the pieces worn (weapon included) add to the attributes.</summary>
    public float equipmentStrength, equipmentDexterity, equipmentArmor, equipmentVitality;
    /// <summary>The weapon's damage and penetration by type.</summary>
    public float pierceDamage, slashDamage, bluntDamage, piercePenetration, slashPenetration, bluntPenetration;
    /// <summary>The armour's defence by type and its health, summed over the pieces.</summary>
    public float pierceDefense, slashDefense, bluntDefense, armorHealth;
    /// <summary>How much each point of strength and of dexterity counts for the class's damage (they add up to 1).</summary>
    public float strengthDamageWeight, dexterityDamageWeight;
}

/// <summary>
/// A hero's horse from the engine (<c>OBJETOS</c>, the mount worn): it replaces the fixed <see cref="MountTuning"/> numbers for that hero.
/// Zero health = no mount from the engine, the tuning's horse.
/// </summary>
[Serializable]
public struct HeroHorseStatsComponent : IComponentData
{
    public float health, trotSpeed, gallopSpeed, stamina;
}

/// <summary>A ticket hero without a mount (Víctor, 2026-11-16): it fights on foot and cannot call a horse.</summary>
public struct HeroOnFootTag : IComponentData { }

/// <summary>
/// How the engine's numbers become the hero's combat stats: the hero rules the engine's author proposed to Conquest (2026-11-11,
/// <c>Trabajo_En_Conquest.md</c> §3.5), with which the numbers of <c>OBJETOS</c> are balanced against the troops (with tier I gear the hero
/// lasts like a tier V unit and hits like a tier III one; with tier V gear, like three tier V units and one). In
/// <see cref="HeroGameplayConfigComponent"/>.
/// </summary>
[Serializable]
public struct HeroEngineRules
{
    [UnityEngine.Tooltip("Vida del héroe sin vitalidad ni armadura (igual en todas las clases).")]
    public float baseHealth;
    [UnityEngine.Tooltip("Vida por punto de vitalidad.")]
    public float healthPerVitality;
    [UnityEngine.Tooltip("Alivio por punto de defensa de un tipo, sobre el daño de ese tipo (0,0025 = 0,25 %).")]
    public float reliefPerDefense;
    [UnityEngine.Tooltip("Alivio de todos los tipos por punto del atributo armadura.")]
    public float reliefPerArmor;
    [UnityEngine.Tooltip("Daño de más por punto del atributo de daño de la clase (0,01 = 1 %).")]
    public float damagePerAttribute;

    public static HeroEngineRules Default => new()
    {
        baseHealth = 1000f, healthPerVitality = 30f, reliefPerDefense = 0.0025f, reliefPerArmor = 0.0025f, damagePerAttribute = 0.01f,
    };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public HeroEngineRules OrDefault() => baseHealth > 0f ? this : Default;
}
