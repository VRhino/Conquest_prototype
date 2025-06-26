using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple helper attached to spawn point buttons on the minimap.
/// </summary>
public class SpawnIcon : MonoBehaviour
{
    int _spawnId;
    MapUIController _controller;

    /// <summary>Initializes the icon with its spawn identifier.</summary>
    public void Initialize(int id, MapUIController controller)
    {
        _spawnId = id;
        _controller = controller;
        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        _controller?.SelectSpawn(_spawnId);
    }
}
