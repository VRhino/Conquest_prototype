using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador de UI para el panel lateral de selección de escuadrones disponibles para agregar al héroe.
/// Instancia prefabs de opciones de escuadrón y maneja la selección.
/// </summary>
public class SquadSelectionPanel : MonoBehaviour
{
    [Header("Panel principal")]
    public GameObject mainPanel;

    [Header("Contenedor de opciones")]
    public Transform squadOptionsContainer;

    [Header("Prefab de opción de escuadrón")]
    public GameObject squadOptionPrefab;

    [Header("Botón de cerrar")]
    public Button closeButton;

    // Callback para notificar selección
    public System.Action<SquadData> OnSquadSelected;



    // Instancia el panel y muestra las opciones según availableSquads del héroe y el tipo de unidad
    public void Open(HeroData heroData, UnitType filterType)
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        // Limpiar opciones previas
        foreach (Transform child in squadOptionsContainer)
            Destroy(child.gameObject);

        // Cargar la base de datos desde Resources/Data/Squads/SquadDatabase
        var squadDatabase = Resources.Load<SquadDatabase>("Data/Squads/SquadDatabase");
        if (squadDatabase == null || heroData == null)
        {
            Debug.LogWarning("[SquadSelectionPanel] Falta SquadDatabase o HeroData");
            return;
        }
        // Mostrar solo los escuadrones disponibles para el héroe Y del tipo solicitado
        foreach (string squadId in heroData.availableSquads)
        {
            var squadData = squadDatabase.allSquads.Find(sq => sq != null && sq.id == squadId && sq.unitType == filterType);
            if (squadData == null)
            {
                // No warning, simplemente no mostrar si no es del tipo
                continue;
            }
            var optionGO = Instantiate(squadOptionPrefab, squadOptionsContainer);
            var optionUI = optionGO.GetComponent<SquadOptionUI>();
            if (optionUI != null)
            {
                optionUI.SetSquadData(squadData);
                optionUI.onClick = () => OnOptionClicked(squadData);
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void Close()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
    }

    private void OnOptionClicked(SquadData squadData)
    {
        Debug.Log($"[SquadSelectionPanel] Seleccionado escuadrón: {squadData.squadName}");
        OnSquadSelected?.Invoke(squadData);
        Close();
    }
}
