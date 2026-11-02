using UnityEngine;

/// <summary>
/// Battle skill bar (H6, H10): the hero's Q, E, R and ultimate, its two active perks (4, 5) and the abilities of its active
/// squad (1, 2, 3), each with its cooldown. It shows <see cref="UIHeroBattleDataComponent.skills"/>, which
/// <see cref="UIBattleDataSystem"/> writes, and a click on a squad slot goes out through <see cref="BattleCommandInput"/>.
/// <see cref="HUDController"/> creates it from <see cref="ResourcePath"/>, so it needs no scene reference.
/// </summary>
public class SkillBarController : MonoBehaviour
{
    public const string ResourcePath = "UI/SkillBar";

    [Tooltip("Un hueco por SkillSlot, de Q a Squad3, en ese orden.")]
    [SerializeField] SkillBarSlot[] slots = new SkillBarSlot[UIHeroBattleDataComponent.SkillSlotCount];

    readonly SkillIconCatalog _icons = new();
    bool _squadsLoaded;
    UIHeroBattleDataComponent _data;

    public SkillIconCatalog Icons => _icons;

    public static SkillBarController Spawn()
    {
        var prefab = Resources.Load<SkillBarController>(ResourcePath);
        return prefab != null ? Instantiate(prefab) : null;
    }

    void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            var slot = (SkillSlot)((int)SkillSlot.Q + i);
            if (BattleCommandInput.IsSquadSlot(slot))
                slots[i].Clicked += () => BattleCommandInput.TryRequestSquadSkill(slot, _data.Skill(slot));
        }
    }

    void Start()
    {
        var hero = PlayerSessionService.SelectedHero;
        if (hero != null) _icons.AddClass(HeroClassManager.GetClassDefinition(hero.classId));
    }

    public void Apply(in UIHeroBattleDataComponent data)
    {
        _data = data;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            var slot = i < data.skills.Length ? data.skills[i] : default;
            if (slot.skillId != 0 && !_icons.Contains(slot.skillId)) LoadSquadIcons();
            slots[i].Show(slot, _icons.Find(slot.skillId));
        }
    }

    /// <summary>Squad abilities are known once, from every squad (the active one changes with swaps).</summary>
    void LoadSquadIcons()
    {
        if (_squadsLoaded) return;
        _squadsLoaded = true;
        var squads = SquadDataService.GetAllSquads();
        if (squads == null) return;
        foreach (var squad in squads) _icons.AddSquad(squad);
    }
}
