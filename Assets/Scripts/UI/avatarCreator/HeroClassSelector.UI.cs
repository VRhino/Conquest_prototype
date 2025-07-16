using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HeroClassSelector : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Transform classContainer;
    [SerializeField] private GameObject classButtonPrefab;

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

    private HeroClassDefinition selectedClass;

    void Start()
    {
        GenerateButtons();
        // Mostrar detalles de la clase en el índice 1 al iniciar (si existe)
        if (availableClasses != null && availableClasses.Count > 1)
        {
            selectedClass = availableClasses[1];
            UpdateDetails(selectedClass);
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

        // Solo actualizar la UI y guardar la selección localmente.
        UpdateDetails(heroClass);

        // (Opcional) actualiza la vista del modelo aquí si tienes un renderizador

        // La asignación de clase y stats al HeroData debe hacerse al crear el héroe en el controlador principal.
    }

    private void UpdateDetails(HeroClassDefinition heroClass)
    {
        
       
        if (classNameText) classNameText.text = heroClass.heroClass.ToString();
        if (classDescriptionText) classDescriptionText.text = heroClass.description;
        if (classIconImage) classIconImage.sprite = heroClass.icon;

        // Asegurar que los valores de stats se actualizan correctamente
        if (vitalityText) vitalityText.text = $"Vitalidad: {heroClass.baseVitality}";
        if (dexterityText) dexterityText.text = $"Destreza: {heroClass.baseDexterity}";
        if (strengthText) strengthText.text = $"Fuerza: {heroClass.baseStrength}";
        if (armorText) armorText.text = $"Armadura: {heroClass.baseArmor}";
    }
}
