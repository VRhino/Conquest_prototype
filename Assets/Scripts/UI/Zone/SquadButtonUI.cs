using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI helper for supply point squad selection buttons.
/// </summary>
public class SquadButtonUI : MonoBehaviour
{
    [SerializeField] TMP_Text _label;
    int _squadId;
    SupplyPointUIController _controller;

    /// <summary>Assigns the squad identifier and owning controller.</summary>
    public void Setup(int squadId, SupplyPointUIController controller)
    {
        _squadId = squadId;
        _controller = controller;
        if (_label != null)
            _label.text = $"Squad {_squadId}";
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        _controller?.RequestSwap(_squadId);
    }
}
