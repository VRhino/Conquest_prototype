using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Data.Avatar;


public class HeroClassSelector : MonoBehaviour
{
    [SerializeField] private List<string> basePartIds = new();
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

    [Header("Avatar DB")]
    private AvatarPartDatabase avatarPartDatabase; // Asignar desde el editor o cargar en runtime

    private HeroClassDefinition selectedClass;


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
        GenerateButtons();
        // Mostrar detalles de la clase en el índice 1 al iniciar (si existe)
        if (availableClasses != null && availableClasses.Count > 1)
        {
            selectedClass = availableClasses[1];
            SelectClass(selectedClass);
        }
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

        // Limpiar dummy y dejar solo piezas base
        ResetModularDummyToBase();

        // Activar visualmente las piezas de armadura por defecto según la clase
        ActivateDefaultArmorVisuals(heroClass);

        // Instanciar perks de la clase
        UpdatePerksUI(heroClass);
        // Rellenar habilidades de la clase
        UpdateAbilitiesUI(heroClass);

        // La asignación de clase y stats al HeroData debe hacerse al crear el héroe en el controlador principal.
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
    // Desactiva todas las piezas de armadura y deja solo las piezas base activas según la lista basePartIds
    private void ResetModularDummyToBase()
    {
        var currentGender = avatarPartSelector.currentGender;
        if (modularDummy == null || avatarPartDatabase == null) return;
        // Primero, desactivar todos los hijos de todos los posibles boneTargets de piezas base
        HashSet<string> boneTargets = new HashSet<string>();
        foreach (var id in basePartIds)
        {
            var part = FindAvatarPartById(id);
            if (part == null || part.attachments == null) continue;
            foreach (var att in part.attachments)
            {
                string boneTarget = currentGender == Gender.Male ? att.boneTargetMale : att.boneTargetFemale;
                if (!string.IsNullOrEmpty(boneTarget)) boneTargets.Add(boneTarget);
            }
        }
        foreach (var boneTarget in boneTargets)
        {
            var boneTransform = modularDummy.Find(boneTarget);
            if (boneTransform == null)
                boneTransform = FindDeepChild(modularDummy, boneTarget);
            if (boneTransform == null) continue;
            foreach (Transform child in boneTransform)
                child.gameObject.SetActive(false);
        }
        // Ahora, activar solo los visualAttachments de las piezas base
        foreach (var id in basePartIds)
        {
            var part = FindAvatarPartById(id);
            if (part == null || part.attachments == null) continue;
            foreach (var att in part.attachments)
            {
                string boneTarget = currentGender == Gender.Male ? att.boneTargetMale : att.boneTargetFemale;
                string prefabName = currentGender == Gender.Male ? att.prefabPathMale : att.prefabPathFemale;
                if (string.IsNullOrEmpty(boneTarget) || string.IsNullOrEmpty(prefabName)) continue;
                var boneTransform = modularDummy.Find(boneTarget);
                if (boneTransform == null)
                    boneTransform = FindDeepChild(modularDummy, boneTarget);
                if (boneTransform == null) continue;
                foreach (Transform child in boneTransform)
                {
                    if (child.name == prefabName)
                        child.gameObject.SetActive(true);
                }
            }
        }
    }

    // Busca una pieza en la base de datos por id (en todas las listas)
    private AvatarPartDefinition FindAvatarPartById(string id)
    {
        if (avatarPartDatabase == null) return null;
        foreach (var list in new List<List<AvatarPartDefinition>> {
            avatarPartDatabase.torsoParts,
            avatarPartDatabase.pantsParts,
            avatarPartDatabase.bootsParts,
            avatarPartDatabase.glovesParts,
            avatarPartDatabase.headParts,
            avatarPartDatabase.faceParts,
            avatarPartDatabase.hairParts,
            avatarPartDatabase.eyebrowsParts,
            avatarPartDatabase.beardParts
        })
        {
            if (list == null) continue;
            var part = list.Find(p => p != null && p.id == id);
            if (part != null) return part;
        }
        return null;
    }

    // Mapea la clase a un set de armadura y activa visualmente las piezas
    private void ActivateDefaultArmorVisuals(HeroClassDefinition heroClass)
    {
        string armorSet = "";
        switch (heroClass.heroClass.ToString())
        {
            case "Bow":
                armorSet = "LigthArmorDefault";
                break;
            case "Spear":
            case "TwoHandedSword":
                armorSet = "MediumArmorDefault";
                break;
            case "SwordAndShield":
                armorSet = "HeavyArmorDefault";
                break;
            default:
                armorSet = "LigthArmorDefault";
                break;
        }

        string torsoId = $"Torso{armorSet}";
        string pantsId = $"Pants{armorSet}";
        string bootsId = $"Boots{armorSet}";
        string glovesId = $"Gloves{armorSet}";

        ActivateArmorPieceById(torsoId);
        ActivateArmorPieceById(pantsId);
        ActivateArmorPieceById(bootsId);
        ActivateArmorPieceById(glovesId);

        Debug.Log($"Activando visuales: {torsoId}, {pantsId}, {bootsId}, {glovesId}");
    }

    // Activa la pieza de armadura por id usando avatarPartDatabase, boneTarget y prefabPath
    private void ActivateArmorPieceById(string partId)
    {
        var currentGender = avatarPartSelector.currentGender;
        if (modularDummy == null)
        {
            Debug.LogWarning("No se asignó la referencia a modularDummy en HeroClassSelector");
            return;
        }
        if (avatarPartDatabase == null)
        {
            Debug.LogWarning("No se asignó la referencia a avatarPartDatabase en HeroClassSelector");
            return;
        }

        // Buscar la pieza en la base de datos (en todas las listas de equipo)
        AvatarPartDefinition partDef = null;
        foreach (var list in new List<List<AvatarPartDefinition>> {
            avatarPartDatabase.torsoParts,
            avatarPartDatabase.pantsParts,
            avatarPartDatabase.bootsParts,
            avatarPartDatabase.glovesParts,
            avatarPartDatabase.headParts
        })
        {
            if (list == null) continue;
            partDef = list.Find(p => p != null && p.id == partId);
            if (partDef != null) break;
        }
        if (partDef == null)
        {
            Debug.LogWarning($"No se encontró la pieza {partId} en la base de datos");
            return;
        }

        if (partDef.attachments == null || partDef.attachments.Count == 0)
        {
            Debug.LogWarning($"La pieza {partId} no tiene visualAttachments");
            return;
        }

        foreach (var attachment in partDef.attachments)
        {
            string boneTarget = currentGender == Gender.Male ? attachment.boneTargetMale : attachment.boneTargetFemale;
            string prefabName = currentGender == Gender.Male ? attachment.prefabPathMale : attachment.prefabPathFemale;
            if (string.IsNullOrEmpty(boneTarget) || string.IsNullOrEmpty(prefabName))
            {
                Debug.LogWarning($"Attachment de {partId} no tiene boneTarget o prefabPath para el género actual");
                continue;
            }
            // Buscar el contenedor (boneTarget) dentro de modularDummy
            var boneTransform = modularDummy.Find(boneTarget);
            if (boneTransform == null)
            {
                // Búsqueda recursiva si no está en el primer nivel
                boneTransform = FindDeepChild(modularDummy, boneTarget);
            }
            if (boneTransform == null)
            {
                Debug.LogWarning($"No se encontró el boneTarget '{boneTarget}' en modularDummy para {partId}");
                continue;
            }
            // Iterar hijos y activar solo el que coincide con prefabName
            bool found = false;
            foreach (Transform child in boneTransform)
            {
                bool activate = child.name == prefabName;
                child.gameObject.SetActive(activate);
                if (activate) found = true;
            }
            if (!found)
            {
                Debug.LogWarning($"No se encontró el prefab '{prefabName}' dentro de '{boneTarget}' para {partId}");
            }
        }
    }

    // Búsqueda recursiva de un hijo por nombre
    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
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
