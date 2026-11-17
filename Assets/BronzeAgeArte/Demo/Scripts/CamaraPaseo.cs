// Cámara para recorrer la galería: WASD camina (a altura fija, sin física: no se cae ni atraviesa nada), Mayús corre,
// el ratón mira solo con el ratón capturado (clic para capturar, Esc para soltar).
// Funciona con el Input System nuevo (Conquest: solo el nuevo) y, si el proyecto lo tuviera, con el antiguo.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BronzeAge.Demo
{
    public class CamaraPaseo : MonoBehaviour
    {
        public float velocidadAndar = 4f;      // m/s
        public float velocidadCorrer = 9f;     // m/s
        public float sensibilidadRaton = 0.12f;
        public float altura = 1.6f;            // altura de los ojos sobre el suelo

        float guinada, cabeceo;

        void Awake()
        {
            // esta es la cámara de la escena: la única con AudioListener y la principal
            gameObject.tag = "MainCamera";
            if (GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
        }

        void Start()
        {
            var e = transform.eulerAngles;
            guinada = e.y;
            cabeceo = e.x > 180 ? e.x - 360 : e.x;
        }

        void Update()
        {
            bool clic = false, esc = false, w = false, a = false, s = false, d = false, mayus = false;
            Vector2 raton = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var m = Mouse.current;
            if (m != null) { clic = m.leftButton.wasPressedThisFrame; raton = m.delta.ReadValue(); }
            if (k != null)
            {
                esc = k.escapeKey.wasPressedThisFrame;
                w = k.wKey.isPressed; a = k.aKey.isPressed; s = k.sKey.isPressed; d = k.dKey.isPressed;
                mayus = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            clic = Input.GetMouseButtonDown(0); esc = Input.GetKeyDown(KeyCode.Escape);
            w = Input.GetKey(KeyCode.W); a = Input.GetKey(KeyCode.A); s = Input.GetKey(KeyCode.S); d = Input.GetKey(KeyCode.D);
            mayus = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            raton = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
            if (clic) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (esc) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                raton *= sensibilidadRaton;
                guinada += raton.x;
                cabeceo = Mathf.Clamp(cabeceo - raton.y, -80f, 80f);
                transform.rotation = Quaternion.Euler(cabeceo, guinada, 0);
            }

            var mov = Vector3.zero;
            if (w) mov += Vector3.forward;
            if (s) mov += Vector3.back;
            if (a) mov += Vector3.left;
            if (d) mov += Vector3.right;
            var p = transform.position;
            if (mov != Vector3.zero)
            {
                float v = mayus ? velocidadCorrer : velocidadAndar;
                p += Quaternion.Euler(0, guinada, 0) * mov.normalized * (v * Time.deltaTime);
            }
            p.y = altura;                       // siempre a la altura de los ojos: sin caídas
            transform.position = p;
        }
    }
}
