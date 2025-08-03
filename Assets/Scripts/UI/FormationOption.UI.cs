using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador UI para mostrar una formación disponible de escuadrón.
/// </summary>
public class FormationOptionUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public Image formationIcon;

    private GridFormationScriptableObject formationData;

    /// <summary>
    /// Asigna los datos visuales de la formación.
    /// </summary>
    public void SetFormation(GridFormationScriptableObject formation)
    {
        formationData = formation;
        if (formationIcon != null && formation != null && formation.formationIcon != null)
            formationIcon.sprite = formation.formationIcon;
    }

    public GridFormationScriptableObject GetFormationData() => formationData;
}
