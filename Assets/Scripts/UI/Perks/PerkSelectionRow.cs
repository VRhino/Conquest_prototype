using TMPro;
using UnityEngine;

/// <summary>One perk in the perk selection panel (H5): icon, name, kind, description and the choose / remove button.</summary>
public class PerkSelectionRow : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image icon;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text kind;
    [SerializeField] TMP_Text description;
    [SerializeField] UnityEngine.UI.Button toggleButton;
    [SerializeField] TMP_Text toggleLabel;
    [SerializeField] UnityEngine.UI.Image background;
    [SerializeField] Color chosenColor = new(0.25f, 0.4f, 0.25f, 0.9f);
    [SerializeField] Color idleColor = new(0.15f, 0.15f, 0.15f, 0.9f);

    public void Bind(HeroPerk perk, bool chosen, bool canAdd, System.Action onToggle)
    {
        if (icon != null)
        {
            icon.sprite = perk.icon;
            icon.enabled = perk.icon != null;
        }
        if (title != null) title.text = perk.perkName;
        if (kind != null) kind.text = perk.isPassive ? "Pasivo" : "Activo";
        if (description != null) description.text = perk.description;
        if (background != null) background.color = chosen ? chosenColor : idleColor;
        if (toggleLabel != null) toggleLabel.text = chosen ? "Quitar" : "Elegir";
        if (toggleButton == null) return;
        toggleButton.interactable = chosen || canAdd;
        toggleButton.onClick.RemoveAllListeners();
        toggleButton.onClick.AddListener(() => onToggle());
    }
}
