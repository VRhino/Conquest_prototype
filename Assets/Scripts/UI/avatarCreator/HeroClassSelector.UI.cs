using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;



using Data.Avatar;
using Data.Items;


public class HeroClassSelector : MonoBehaviour
{
    private List<string> baseItemIds = new();
    // Rellena baseItemIds según la clase seleccionada
    [Header("Modelo 3D")]
    [SerializeField] private Transform modularDummy; // Asignar desde el editor
    [Header("Setup")]
    [SerializeField] private Transform classContainer;
    [SerializeField] private GameObject classButtonPrefab;    

    [Header("Perks UI")]
    [SerializeField] private Transform perksContainer; // Asignar desde el editor
    [SerializeField] private GameObject perkItemPrefab; // Prefab con hijos "Icon" (Image) y "Label" (TMP_Text)
    [Header("Abilities UI")]
    [SerializeField] private Transform abilitiesContainer; // Asignar desde el editor
    [SerializeField] private string qAbilityName = "QAbility";
    [SerializeField] private string eAbilityName = "EAbility";
    [SerializeField] private string rAbilityName = "RAbility";
    [SerializeField] private string ultimateAbilityName = "Ultimate";

    [Header("UI de detalles")]
    [SerializeField] private TMP_Text classNameText;
    [SerializeField] private Image classIconImage;
    [SerializeField] private TMP_Text classDescriptionText;
    [SerializeField] private TMP_Text vitalityText;
    [SerializeField] private TMP_Text dexterityText;
    [SerializeField] private TMP_Text strengthText;
    [SerializeField] private TMP_Text armorText;

    [Header("Data")]
    public List<HeroClassDefinition> availableClasses = new();

    [Header("Character Selector")]
    [SerializeField] private AvatarPartSelector avatarPartSelector;

    
    private AvatarPartDatabase avatarPartDatabase;
    private ItemDatabase itemDB; 
    public HeroClassDefinition selectedClass;


    void Start()
    {
        // Cargar la base de datos desde Resources si no está asignada
        if (avatarPartDatabase == null)
        {
            avatarPartDatabase = Resources.Load<AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
            if (avatarPartDatabase == null)
            {
                Debug.LogError("No se pudo cargar AvatarPartDatabase desde Resources/Data/Avatar/AvatarPartDatabase");
            }
        }
        if (itemDB == null)
        {
            itemDB = Resources.Load<ItemDatabase>("Data/Items/ItemDatabase");
            if (itemDB == null)
            {
                Debug.LogError("No se pudo cargar ItemDatabase desde Resources/Data/Items/ItemDatabase");
            }
        }
        GenerateButtons();
        // Mostrar detalles de la clase en el índice 1 al iniciar (si existe)
        if (availableClasses != null && availableClasses.Count > 1)
        {
            selectedClass = availableClasses[1];
            SelectClass(selectedClass);
        }
    }
    public Equipment GetCurrentEquipment()
    {
        // Devuelve el equipo actual usando los itemId seleccionados
        Equipment equipment = new Equipment();
        // Asume que baseItemIds tiene el orden: weapon, helmet, torso, gloves, pants
        if (baseItemIds.Count > 0) equipment.weaponId = baseItemIds[0];
        if (baseItemIds.Count > 1) equipment.helmetId = baseItemIds[1];
        if (baseItemIds.Count > 2) equipment.torsoId = baseItemIds[2];
        if (baseItemIds.Count > 3) equipment.glovesId = baseItemIds[3];
        if (baseItemIds.Count > 4) equipment.pantsId = baseItemIds[4];
        return equipment;
    }

     private void FillBaseItemIdsForClass(HeroClassDefinition heroClass)
    {
        baseItemIds.Clear();
        // Determina el sufijo de armadura y el itemId de arma según la clase
        string armorSuffix, weaponId;
        switch (heroClass.heroClass.ToString())
        {
            case "Bow":
                armorSuffix = "L";
                weaponId = "WeaBBasic";
                break;
            case "Spear":
                armorSuffix = "M";
                weaponId = "WeaSBasic";
                break;
            case "TwoHandedSword":
                armorSuffix = "M";
                weaponId = "WeaS2HBasic";
                break;
            case "SwordAndShield":
                armorSuffix = "H";
                weaponId = "WeaS&SBasic";
                break;
            default:
                armorSuffix = "L";
                weaponId = "WeaBBasic";
                break;
        }
        baseItemIds.Add(weaponId);
        baseItemIds.Add($"Boot{armorSuffix}ADef");
        baseItemIds.Add($"Tor{armorSuffix}ADef");
        baseItemIds.Add($"Glo{armorSuffix}ADef");
        baseItemIds.Add($"Pan{armorSuffix}ADef");
    }
    private void GenerateButtons()
    {
        foreach (Transform child in classContainer)
            Destroy(child.gameObject);

        foreach (var heroClass in availableClasses)
        {
            var buttonGO = Instantiate(classButtonPrefab, classContainer);
            var iconTransform = buttonGO.transform.Find("Icon");
            var labelTransform = buttonGO.transform.Find("Label");
            var icon = iconTransform ? iconTransform.GetComponent<Image>() : null;
            var label = labelTransform ? labelTransform.GetComponent<TMP_Text>() : null;
            var button = buttonGO.GetComponent<Button>();

            if (!icon)
                Debug.LogWarning($"El prefab del botón de clase no tiene un hijo 'Icon' con componente Image. ({buttonGO.name})");
            else
                icon.sprite = heroClass.icon;

            if (!label)
                Debug.LogWarning($"El prefab del botón de clase no tiene un hijo 'Label' con componente TMP_Text. ({buttonGO.name})");
            else
                label.text = heroClass.heroClass.ToString();

            if (!button)
                Debug.LogWarning($"El prefab del botón de clase no tiene componente Button. ({buttonGO.name})");
            else
                button.onClick.AddListener(() => SelectClass(heroClass));
        }
    }

    private void SelectClass(HeroClassDefinition heroClass)
    {
        selectedClass = heroClass;

        // Actualizar la UI y guardar la selección localmente.
        UpdateDetails(heroClass);

        // Limpiar dummy y dejar solo piezas base visuales
        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(
            modularDummy,
            avatarPartDatabase,
            new List<string>(),
            avatarPartSelector.currentGender
        );

           // Rellenar baseItemIds según la clase seleccionada
        FillBaseItemIdsForClass(heroClass);

        // Activar visualmente las piezas de armadura por defecto según la clase
        ActivateDefaultArmorVisuals(heroClass);
        // Instanciar perks de la clase
        UpdatePerksUI(heroClass);
        // Rellenar habilidades de la clase
        UpdateAbilitiesUI(heroClass);
    }

    // Instancia los perks de la clase seleccionada en el perksContainer
    private void UpdatePerksUI(HeroClassDefinition heroClass)
    {
        if (perksContainer == null || perkItemPrefab == null)
            return;

        // Limpiar perks previos
        foreach (Transform child in perksContainer)
            Destroy(child.gameObject);

        if (heroClass.validClassPerks == null) return;
        foreach (var perk in heroClass.validClassPerks)
        {
            if (perk == null) continue;
            var perkGO = Instantiate(perkItemPrefab, perksContainer);
            var iconTransform = perkGO.transform.Find("Icon");
            var labelTransform = perkGO.transform.Find("Label");
            var descTransform = perkGO.transform.Find("Description");
            var icon = iconTransform ? iconTransform.GetComponent<Image>() : null;
            var label = labelTransform ? labelTransform.GetComponent<TMP_Text>() : null;
            var desc = descTransform ? descTransform.GetComponent<TMP_Text>() : null;
            if (icon) icon.sprite = perk.icon;
            if (label) label.text = perk.perkName;
            if (desc) desc.text = perk.description;
        }
    }
    // Rellena los datos de las habilidades en los contenedores Q, E, R y Ultimate
    private void UpdateAbilitiesUI(HeroClassDefinition heroClass)
    {
        if (abilitiesContainer == null || heroClass.abilities == null) return;

        // Buscar los 4 contenedores hijos por nombre
        var qGO = abilitiesContainer.Find(qAbilityName);
        var eGO = abilitiesContainer.Find(eAbilityName);
        var rGO = abilitiesContainer.Find(rAbilityName);
        var uGO = abilitiesContainer.Find(ultimateAbilityName);

        // Limpiar todos los campos primero
        void ClearAbility(Transform abilityGO)
        {
            if (abilityGO == null) return;
            var icon = abilityGO.Find("Icon")?.GetComponent<Image>();
            var label = abilityGO.Find("Label")?.GetComponent<TMPro.TMP_Text>();
            var desc = abilityGO.Find("Description")?.GetComponent<TMPro.TMP_Text>();
            if (icon) icon.sprite = null;
            if (label) label.text = "";
            if (desc) desc.text = "";
        }
        ClearAbility(qGO);
        ClearAbility(eGO);
        ClearAbility(rGO);
        ClearAbility(uGO);

        // Map category string to container
        System.Action<HeroAbility, Transform> FillAbility = (ability, abilityGO) => {
            if (ability == null || abilityGO == null) return;
            var icon = abilityGO.Find("Icon")?.GetComponent<Image>();
            var label = abilityGO.Find("Label")?.GetComponent<TMPro.TMP_Text>();
            var desc = abilityGO.Find("Description")?.GetComponent<TMPro.TMP_Text>();
            if (icon) icon.sprite = ability.icon;
            if (label) label.text = ability.abilityName;
            if (desc) desc.text = ability.description;
        };

        foreach (var ability in heroClass.abilities)
        {
            if (ability == null) continue;
            switch (ability.category)
            {
                case HeroAbilityCategory.Q:
                    FillAbility(ability, qGO);
                    break;
                case HeroAbilityCategory.E:
                    FillAbility(ability, eGO);
                    break;
                case HeroAbilityCategory.R:
                    FillAbility(ability, rGO);
                    break;
                case HeroAbilityCategory.Ultimate:
                    FillAbility(ability, uGO);
                    break;
            }
        }
    }

    // Activa visualmente las piezas de armadura por itemId usando AvatarVisualUtils
    private void ActivateDefaultArmorVisuals(HeroClassDefinition heroClass)
    {
        foreach (var itemId in baseItemIds)
        {
            var itemData = itemDB.GetItemDataById(itemId);
            if (itemData != null && !string.IsNullOrEmpty(itemData.visualPartId))
            {
                Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(
                    modularDummy,
                    avatarPartDatabase,
                    itemData.visualPartId,
                    avatarPartSelector.currentGender
                );
            }
        }
    }
    private void UpdateDetails(HeroClassDefinition heroClass)
    {
        
       
        if (classNameText) classNameText.text = heroClass.heroClass.ToString();
        if (classDescriptionText) classDescriptionText.text = heroClass.description;
        if (classIconImage) classIconImage.sprite = heroClass.icon;

        // Asegurar que los valores de stats se actualizan correctamente
        if (vitalityText) vitalityText.text = $"Vitality: {heroClass.baseVitality}";
        if (dexterityText) dexterityText.text = $"Dexterity: {heroClass.baseDexterity}";
        if (strengthText) strengthText.text = $"Strength: {heroClass.baseStrength}";
        if (armorText) armorText.text = $"Armor: {heroClass.baseArmor}";
    }
}
