using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Radial menu of the battle (GDD 12.3, H10). While ALT is held it shows the abilities of the active squad and its
/// formations, and the cursor is free for the UI (<see cref="BattleCommandInput.PointerMode"/>); nothing pauses. A click on
/// a sector or releasing ALT over it asks for that command, and releasing it in the centre cancels. It reads
/// <see cref="UIHeroBattleDataComponent"/> and never writes ECS. <see cref="HUDController"/> creates it from
/// <see cref="ResourcePath"/>.
/// </summary>
public class BattleRadialMenu : MonoBehaviour
{
    public const string ResourcePath = "UI/BattleRadialMenu";

    const int SkillSectors = SkillSlot.Squad3 - SkillSlot.Squad1 + 1;
    const int FormationSectors = 4; // F1-F4
    const int SectorCount = SkillSectors + FormationSectors;
    /// <summary>The second ability goes straight up, between the other two.</summary>
    const float FirstSectorAngle = -360f / SectorCount;

    [SerializeField] RectTransform panel;
    [Tooltip("Imagen rellena de un sector, que gira hasta el sector bajo el puntero.")]
    [SerializeField] Image sectorHighlight;
    [Tooltip("Nombre de lo que hay bajo el puntero, en el centro de la rueda.")]
    [SerializeField] TMP_Text centerLabel;
    [Tooltip("Uno por hueco de escuadra, de Squad1 a Squad3.")]
    [SerializeField] SkillBarSlot[] skills = new SkillBarSlot[SkillSectors];
    [Tooltip("Uno por formación, de F1 a F4.")]
    [SerializeField] HUDFormationIconController[] formations = new HUDFormationIconController[FormationSectors];

    [Header("Tamaño, en unidades del canvas")]
    [Min(0f)] [SerializeField] float sectorRadius = 210f;
    [Tooltip("Dentro de este radio no hay sector: soltar aquí cancela.")]
    [Min(0f)] [SerializeField] float deadZone = 90f;
    [Tooltip("Fuera de este radio tampoco hay sector, para no elegir nada al ir a la barra.")]
    [Min(0f)] [SerializeField] float outerRadius = 300f;

    SquadData _squad;
    UIHeroBattleDataComponent _data;
    int _hovered = RadialMenuLayout.NoSector;
    /// <summary>The sector a click already asked for: releasing ALT over it does not ask again.</summary>
    int _clicked = RadialMenuLayout.NoSector;
    bool _open;
    /// <summary>Focus went away with ALT down (Alt+Tab): nothing opens or launches until ALT is released.</summary>
    bool _waitForRelease;

    public static BattleRadialMenu Spawn()
    {
        var prefab = Resources.Load<BattleRadialMenu>(ResourcePath);
        return prefab != null ? Instantiate(prefab) : null;
    }

    void Awake()
    {
        for (int i = 0; i < SectorCount; i++)
            SectorTransform(i).anchoredPosition = RadialMenuLayout.SectorCenter(i, SectorCount, FirstSectorAngle, sectorRadius);
        sectorHighlight.fillAmount = 1f / SectorCount;
        panel.gameObject.SetActive(false);
    }

    void OnDestroy() => BattleCommandInput.SetPointerMode(false);

    void OnApplicationFocus(bool focused)
    {
        if (!focused) _waitForRelease = true;
    }

    /// <summary>Called by the HUD every frame, before the input systems read what the menu asked for.</summary>
    public void Apply(in UIHeroBattleDataComponent data, SquadData squad, SkillIconCatalog icons)
    {
        _data = data;
        var keyboard = Keyboard.current;
        bool held = keyboard != null && keyboard.altKey.isPressed;
        bool usable = data.heroAlive && !DialogueUIState.IsDialogueOpen && !_waitForRelease;

        if (held && usable && !_open) Open();
        else if (_open && !usable) Close();
        else if (_open && !held)
        {
            if (_hovered != _clicked) Request(_hovered);
            Close();
        }
        if (!held) _waitForRelease = false;
        if (!_open) return;

        BindFormations(squad);
        ShowSkills(icons);
        UpdateHover(icons);
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Request(_hovered)) _clicked = _hovered;
    }

    void Open()
    {
        _open = true;
        _hovered = _clicked = RadialMenuLayout.NoSector;
        panel.gameObject.SetActive(true);
        BattleCommandInput.SetPointerMode(true);
    }

    void Close()
    {
        _open = false;
        panel.gameObject.SetActive(false);
        BattleCommandInput.SetPointerMode(false);
    }

    void BindFormations(SquadData squad)
    {
        bool changed = squad != _squad;
        _squad = squad;
        for (int i = 0; i < formations.Length; i++)
        {
            var definition = Formation(i);
            if (changed)
            {
                formations[i].gameObject.SetActive(definition != null);
                if (definition != null) formations[i].Initialize(definition, i);
            }
            if (definition != null) formations[i].SetActive(definition.formationType == _data.currentFormation);
        }
    }

    void ShowSkills(SkillIconCatalog icons)
    {
        for (int i = 0; i < skills.Length; i++)
        {
            var data = _data.Skill(SquadSlot(i));
            skills[i].Show(data, icons?.Find(data.skillId));
        }
    }

    void UpdateHover(SkillIconCatalog icons)
    {
        var mouse = Mouse.current;
        int sector = RadialMenuLayout.NoSector;
        if (mouse != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, mouse.position.ReadValue(), null, out var local))
            sector = RadialMenuLayout.SectorAt(local, SectorCount, FirstSectorAngle, deadZone, outerRadius);
        if (!HasCommand(sector)) sector = RadialMenuLayout.NoSector;
        if (sector != _hovered) _clicked = RadialMenuLayout.NoSector;
        _hovered = sector;

        bool any = sector != RadialMenuLayout.NoSector;
        sectorHighlight.enabled = any;
        centerLabel.text = any ? SectorName(sector, icons) : string.Empty;
        if (!any) return;
        // The filled image starts at the top and turns clockwise, and a positive Z turns it the other way.
        float halfSector = 180f / SectorCount;
        float angle = RadialMenuLayout.SectorAngle(sector, SectorCount, FirstSectorAngle);
        sectorHighlight.rectTransform.localEulerAngles = new Vector3(0f, 0f, halfSector - angle);
    }

    bool HasCommand(int sector)
    {
        if (sector == RadialMenuLayout.NoSector) return false;
        return sector < SkillSectors ? _data.Skill(SquadSlot(sector)).skillId != 0 : Formation(sector - SkillSectors) != null;
    }

    bool Request(int sector)
    {
        if (!HasCommand(sector)) return false;
        if (sector >= SkillSectors) return BattleCommandInput.TryRequestFormation(sector - SkillSectors);
        var slot = SquadSlot(sector);
        return BattleCommandInput.TryRequestSquadSkill(slot, _data.Skill(slot));
    }

    string SectorName(int sector, SkillIconCatalog icons) => sector < SkillSectors
        ? icons?.FindName(_data.Skill(SquadSlot(sector)).skillId)
        : Formation(sector - SkillSectors).formationType.ToString();

    static SkillSlot SquadSlot(int sector) => (SkillSlot)((int)SkillSlot.Squad1 + sector);

    FormationDefinition Formation(int index) =>
        _squad != null && _squad.formations != null && index < _squad.formations.Length ? _squad.formations[index] : null;

    RectTransform SectorTransform(int sector) => (RectTransform)(sector < SkillSectors
        ? skills[sector].transform
        : formations[sector - SkillSectors].transform);
}
