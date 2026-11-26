using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>La cámara del banco de pruebas de la ciudad (`PlazaNivel1`), en Play: se anda con WASD a la altura de los ojos del héroe (Q y E
/// bajan y suben, Mayúsculas corre) y se mira con el botón derecho del ratón pulsado. Solo para el banco: en el juego la cámara es la del héroe.</summary>
public sealed class CamaraLibreDeBanco : MonoBehaviour
{
    public float Paso = 4.5f, Giro = 0.15f;
    float guinada, cabeceo;

    void Start()
    {
        var e = transform.eulerAngles;
        guinada = e.y;
        cabeceo = e.x > 180 ? e.x - 360 : e.x;
    }

    void Update()
    {
        var t = Keyboard.current;
        var r = Mouse.current;
        if (t == null || r == null) return;
        if (r.rightButton.isPressed)
        {
            var d = r.delta.ReadValue();
            guinada += d.x * Giro;
            cabeceo = Mathf.Clamp(cabeceo - d.y * Giro, -85, 85);
            transform.rotation = Quaternion.Euler(cabeceo, guinada, 0);
        }
        var mover = new Vector3((t.dKey.isPressed ? 1 : 0) - (t.aKey.isPressed ? 1 : 0), (t.eKey.isPressed ? 1 : 0) - (t.qKey.isPressed ? 1 : 0),
            (t.wKey.isPressed ? 1 : 0) - (t.sKey.isPressed ? 1 : 0));
        var plano = Quaternion.Euler(0, guinada, 0);
        transform.position += plano * mover * Paso * (t.leftShiftKey.isPressed ? 3 : 1) * Time.deltaTime;
    }
}
