using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Fief panel to choose the hero's perks (H5, GDD 6.2): lists the class perks the hero has unlocked and writes the chosen
/// ones to the active loadout (<see cref="PerkSelection"/>), saved at once, within 5 passive and 2 active. Key K opens
/// it; <see cref="FullscreenPanelManager"/> loads it from <see cref="ResourcePath"/> and keeps it exclusive with the other
/// fief panels. The talent tree and its points come later.
/// </summary>
public class PerkSelectionPanelController : MonoBehaviour, IFullscreenPanel
{
    /// <summary>Where the panel prefab lives, so it needs no scene reference.</summary>
    public const string ResourcePath = "UI/PerkSelectionPanel";

    [SerializeField] GameObject mainPanel;
    [SerializeField] TMP_Text passiveCounter;
    [SerializeField] TMP_Text activeCounter;
    [SerializeField] TMP_Text emptyMessage;
    [SerializeField] Transform rowContainer;
    [SerializeField] PerkSelectionRow rowTemplate;
    [SerializeField] UnityEngine.UI.Button closeButton;

    readonly List<PerkSelectionRow> _rows = new();
    List<HeroPerk> _available = new();

    public bool IsPanelOpen => mainPanel != null && mainPanel.activeSelf;

    void Awake()
    {
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(() =>
            {
                if (FullscreenPanelManager.Instance != null) FullscreenPanelManager.Instance.ClosePanel<PerkSelectionPanelController>();
                else ClosePanel();
            });
    }

    public void OpenPanel()
    {
        if (mainPanel == null) return;
        mainPanel.SetActive(true);
        Refresh();
    }

    public void ClosePanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
    }

    public void TogglePanel()
    {
        if (IsPanelOpen) ClosePanel();
        else OpenPanel();
    }

    void Refresh()
    {
        var hero = PlayerSessionService.SelectedHero;
        var classDefinition = hero != null ? HeroClassManager.GetClassDefinition(hero.classId) : null;
        _available = classDefinition != null ? PerkSelection.Available(classDefinition.validClassPerks, hero) : new List<HeroPerk>();

        while (_rows.Count < _available.Count)
            _rows.Add(Instantiate(rowTemplate, rowContainer));
        for (int i = 0; i < _rows.Count; i++)
        {
            bool used = i < _available.Count;
            _rows[i].gameObject.SetActive(used);
            if (!used) continue;
            var perk = _available[i];
            _rows[i].Bind(perk, PerkSelection.IsChosen(hero, perk), PerkSelection.CanAdd(hero, _available, perk), () => Toggle(perk));
        }

        PerkSelection.Count(hero, _available, out int passives, out int actives);
        if (passiveCounter != null) passiveCounter.text = $"Pasivos {passives}/{PerkLoadout.MaxPassive}";
        if (activeCounter != null) activeCounter.text = $"Activos {actives}/{PerkLoadout.MaxActive}";
        if (emptyMessage != null) emptyMessage.gameObject.SetActive(_available.Count == 0);
    }

    void Toggle(HeroPerk perk)
    {
        var hero = PlayerSessionService.SelectedHero;
        if (!PerkSelection.Toggle(hero, _available, perk)) return;
        if (PlayerSessionService.CurrentPlayer != null) SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
        Refresh();
    }
}
