using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class OrbitCameraController : MonoBehaviour
{
  public Transform target;
  public float distance = 3.5f;
  public float xSpeed = 120f;
  public float ySpeed = 80f;
  public float yMinLimit = -10f;
  public float yMaxLimit = 70f;

  private float x = 0f;
  private float y = 20f;

  private Vector2 lastMouseInput;
  private bool isDragging = false;

  void Start()
  {
    if (target == null)
      target = GameObject.Find("HeroPreviewRoot")?.transform;

    Vector3 angles = transform.eulerAngles;
    x = angles.y;
    y = angles.x;
  }

  void Update()
  {
    // Desactiva arrastre si el mouse está sobre UI
    if (EventSystem.current.IsPointerOverGameObject())
    {
      isDragging = false;
      return;
    }

    // Solo activa rotación si se hace clic izquierdo fuera de UI
    isDragging = Mouse.current.leftButton.isPressed;

    if (isDragging)
    {
      Vector2 delta = Mouse.current.delta.ReadValue();

      x += delta.x * xSpeed * Time.deltaTime;
      y -= delta.y * ySpeed * Time.deltaTime;
      y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
    }
  }

  void LateUpdate()
  {
    if (target == null) return;

    Quaternion rotation = Quaternion.Euler(y, x, 0);
    Vector3 position = rotation * new Vector3(0, 0, -distance) + target.position;

    transform.rotation = rotation;
    transform.position = position;
  }
}