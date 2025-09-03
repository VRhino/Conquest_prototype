using System;

/// <summary>
/// Representa los atributos base del héroe sin modificaciones temporales ni bonificaciones de equipamiento.
/// Estos valores se guardan directamente en la persistencia y representan el "estado puro" del héroe.
/// </summary>
[Serializable]
public struct HeroBaseStats
{
    /// <summary>Base strength value without equipment or temporary bonuses.</summary>
    public int baseStrength;
    
    /// <summary>Base dexterity value without equipment or temporary bonuses.</summary>
    public int baseDexterity;
    
    /// <summary>Base armor value without equipment or temporary bonuses.</summary>
    public int baseArmor;
    
    /// <summary>Base vitality value without equipment or temporary bonuses.</summary>
    public int baseVitality;

    /// <summary>
    /// Creates HeroBaseStats from HeroData.
    /// </summary>
    /// <param name="heroData">Source hero data</param>
    /// <returns>Base stats structure</returns>
    public static HeroBaseStats FromHeroData(HeroData heroData)
    {
        if (heroData == null)
        {
            return new HeroBaseStats
            {
                baseStrength = 0,
                baseDexterity = 0,
                baseArmor = 0,
                baseVitality = 0
            };
        }

        return new HeroBaseStats
        {
            baseStrength = heroData.strength,
            baseDexterity = heroData.dexterity,
            baseArmor = heroData.armor,
            baseVitality = heroData.vitality
        };
    }
}
