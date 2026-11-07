using TMPro;
using UnityEngine;

/// <summary>
/// Battle skill bar (H6, H10): the hero's Q, E, R and ultimate, its two active perks (4, 5) and the abilities of its active
/// squad (1, 2, 3), each with its cooldown, and the hero's combo on the ultimate (H16). It shows
/// <see cref="UIHeroBattleDataComponent"/>, which <see cref="UIBattleDataSystem"/> writes, and a click on a squad slot goes
/// out through <see cref="BattleCommandInput"/>.
/// <see cref="HUDController"/> creates it from <see cref="ResourcePath"/>, so it needs no scene reference.
/// </summary>
public class SkillBarController : MonoBehaviour
{
    public const string ResourcePath = "UI/SkillBar";

    [Tooltip("Un hueco por SkillSlot, de Q a Squad3, en ese orden.")]
    [SerializeField] SkillBarSlot[] slots = new SkillBarSlot[UIHeroBattleDataComponent.SkillSlotCount];

    [Tooltip("Franja del hueco de la definitiva con el contador; oculta si la definitiva no cuesta combo.")]
    [SerializeField] GameObject comboBadge;
    [Tooltip("Contador de combo de la definitiva: combo actual / coste.")]
    [SerializeField] TMP_Text comboCounter;

    readonly SkillIconCatalog _icons = new();
    int _shownCombo = -1, _shownComboCost = -1;
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
            slots[i].Show(slot, _icons.Find(slot.skillId), data.CanAfford((SkillSlot)((int)SkillSlot.Q + i)));
        }
        ShowCombo(data);
    }

    /// <summary>The text changes only when the whole numbers do, so a steady combo builds no string per frame.</summary>
    void ShowCombo(in UIHeroBattleDataComponent data)
    {
        if (comboBadge == null || comboCounter == null) return;
        bool shown = data.ultimateComboCost > 0f;
        if (comboBadge.activeSelf != shown) comboBadge.SetActive(shown);
        if (!shown) return;
        int combo = Mathf.FloorToInt(data.combo), cost = Mathf.CeilToInt(data.ultimateComboCost);
        if (combo == _shownCombo && cost == _shownComboCost) return;
        _shownCombo = combo;
        _shownComboCost = cost;
        comboCounter.text = combo + "/" + cost;
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
