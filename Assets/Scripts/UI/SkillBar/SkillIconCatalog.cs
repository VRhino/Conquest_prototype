using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Icon of each skill the bar can show, by <see cref="SkillBlob.id"/> (H6, H10): the class abilities and active perks of
/// the local hero, and the squad abilities. The ids are the ones <see cref="SkillBlobBuilder"/> gives the skills.
/// </summary>
public class SkillIconCatalog
{
    readonly Dictionary<int, Sprite> _icons = new();

    public void AddClass(HeroClassDefinition definition)
    {
        if (definition == null) return;
        if (definition.abilities != null)
            foreach (var ability in definition.abilities)
                if (ability != null) _icons[SkillBlobBuilder.IdOf(ability)] = ability.icon;
        if (definition.validClassPerks != null)
            foreach (var perk in definition.validClassPerks)
                if (perk != null) _icons[SkillBlobBuilder.PerkId(perk)] = perk.icon;
    }

    public void AddSquad(SquadData squad)
    {
        if (squad == null || squad.abilitiesByLevel == null) return;
        foreach (var ability in squad.abilitiesByLevel)
            if (ability != null) _icons[SkillBlobBuilder.IdOf(ability)] = ability.icon;
    }

    /// <summary>The skill's icon, or null when it has none or is unknown.</summary>
    public Sprite Find(int skillId) => skillId != 0 && _icons.TryGetValue(skillId, out var icon) ? icon : null;

    public bool Contains(int skillId) => _icons.ContainsKey(skillId);
}
