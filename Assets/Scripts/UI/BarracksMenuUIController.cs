using UnityEngine;

using UnityEngine.UI;

public class BarracksMenuUIController : MonoBehaviour
{
    // Puedes asignar referencias a paneles, textos, botones, etc. desde el editor
    [Header("Panel principal del menú de barracas")]
    public GameObject mainPanel;

    [Header("Botones de acción")]
    public Button exitButton;
    public Button addUnitType1Button;
    public Button addUnitType2Button;
    public Button addUnitType3Button;

    [Header("Textos de información")]
    public TMPro.TextMeshProUGUI expText;
    public TMPro.TextMeshProUGUI barracksSlotsText;

    // Lógica para abrir el menú con la info del héroe
    public void Open(HeroData heroData)
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        // Asignar listeners (solo una vez)
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(Close);
        }
        if (addUnitType1Button != null)
        {
            addUnitType1Button.onClick.RemoveAllListeners();
            addUnitType1Button.onClick.AddListener(() => OnAddUnitClicked(1, heroData));
        }
        if (addUnitType2Button != null)
        {
            addUnitType2Button.onClick.RemoveAllListeners();
            addUnitType2Button.onClick.AddListener(() => OnAddUnitClicked(2, heroData));
        }
        if (addUnitType3Button != null)
        {
            addUnitType3Button.onClick.RemoveAllListeners();
            addUnitType3Button.onClick.AddListener(() => OnAddUnitClicked(3, heroData));
        }

        // Actualizar textos de experiencia y espacios
        if (expText != null && heroData != null)
        {
            // expText.text = $"EXP Unidad: {heroData.sharedUnitExp}/{heroData.maxSharedUnitExp}";
        }
        if (barracksSlotsText != null && heroData != null)
        {
            // barracksSlotsText.text = $"Espacios: {heroData.barracksSlotsUsed}/{heroData.barracksSlotsTotal}";
        }

        Debug.Log($"[BarracksMenuUIController] Abriendo menú de barracas para: {heroData?.heroName ?? "(null)"}");
    }

    public void Close()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
            DialogueUIState.IsDialogueOpen = false;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(true);
    }

    // Lógica para agregar unidad de tipo específico
    public void OnAddUnitClicked(int unitType, HeroData heroData)
    {
        Debug.Log($"[BarracksMenuUIController] Add unidad tipo {unitType} para héroe: {heroData?.heroName ?? "(null)"}");
        // Aquí va la lógica real de agregar la unidad
        // ...
    }
}
