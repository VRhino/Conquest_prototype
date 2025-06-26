using UnityEngine;

/// <summary>
/// Configures the camera used to render the minimap.
/// Should be placed high above the map and set to orthographic mode.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapCamera : MonoBehaviour
{
    [SerializeField] float height = 50f;
    [SerializeField] LayerMask minimapMask;

    void Awake()
    {
        var cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.cullingMask = minimapMask;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var pos = transform.position;
        pos.y = height;
        transform.position = pos;
    }
}
