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
        
        // ItemDatabase ahora usa singleton - verificar que esté disponible
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("ItemDatabase.Instance is null! Make sure ItemDatabase exists in Resources folder.");
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
        Equipment equipment = new Equipment();
        
        // Verificar que ItemDatabase esté disponible antes de crear items
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("[HeroClassSelector] ItemDatabase not available, cannot create equipment instances");
            return equipment;
        }
        
        // Crear InventoryItems usando ItemInstanceService para generar stats e instanceId únicos
        if (baseItemIds.Count > 0 && !string.IsNullOrEmpty(baseItemIds[0]))
            equipment.weapon = CreateEquipmentItem(baseItemIds[0]);
        if (baseItemIds.Count > 1 && !string.IsNullOrEmpty(baseItemIds[1]))
            equipment.helmet = CreateEquipmentItem(baseItemIds[1]);
        if (baseItemIds.Count > 2 && !string.IsNullOrEmpty(baseItemIds[2]))
            equipment.torso = CreateEquipmentItem(baseItemIds[2]);
        if (baseItemIds.Count > 3 && !string.IsNullOrEmpty(baseItemIds[3]))
            equipment.gloves = CreateEquipmentItem(baseItemIds[3]);
        if (baseItemIds.Count > 4 && !string.IsNullOrEmpty(baseItemIds[4]))
            equipment.pants = CreateEquipmentItem(baseItemIds[4]);
        if (baseItemIds.Count > 5 && !string.IsNullOrEmpty(baseItemIds[5]))
            equipment.boots = CreateEquipmentItem(baseItemIds[5]);
            
        return equipment;
    }

    /// <summary>
    /// Crea una instancia de InventoryItem usando ItemInstanceService para generar stats únicos.
    /// Incluye validación y manejo de errores para casos donde el item no existe.
    /// </summary>
    /// <param name="itemId">ID del ítem a crear</param>
    /// <returns>InventoryItem con stats generados o null si hay error</returns>
    private InventoryItem CreateEquipmentItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("[HeroClassSelector] Cannot create equipment item: itemId is null or empty");
            return null;
        }
        
        // Verificar que el item existe en la base de datos antes de crear la instancia
        var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
        if (itemData == null)
        {
            Debug.LogError($"[HeroClassSelector] ItemData not found for ID: {itemId}. Item will not be equipped.");
            return null;
        }
        
        // Crear instancia usando ItemInstanceService para generar stats e instanceId
        var item = ItemInstanceService.CreateItem(itemId);
        if (item == null)
        {
            Debug.LogError($"[HeroClassSelector] Failed to create item instance for ID: {itemId}");
            return null;
        }
        
        Debug.Log($"[HeroClassSelector] Created equipment item: {itemId} with instanceId: {item.instanceId}");
        return item;
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
        baseItemIds.Add("");
        baseItemIds.Add($"Tor{armorSuffix}ADef");
        baseItemIds.Add($"Glo{armorSuffix}ADef");
        baseItemIds.Add($"Pan{armorSuffix}ADef");
        baseItemIds.Add($"Boot{armorSuffix}ADef");
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
            var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
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
