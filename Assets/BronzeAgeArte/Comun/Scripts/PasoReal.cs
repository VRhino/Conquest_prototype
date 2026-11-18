using UnityEngine;

namespace BronzeAge
{
    /// <summary>
    /// Que los pies no patinen (Víctor, 18-11): la velocidad REAL del soldado (lo que se mueve su visual, en m/s, en el
    /// suelo) para el árbol de locomoción de las levas, que tiene sus clips en sus velocidades de autor (andar
    /// <see cref="ANDAR"/>, correr <see cref="CORRER"/>). Por encima de la de correr, el clip de correr se reproduce más
    /// deprisa (<c>Paso</c> = velocidad / correr, acotado a <see cref="PASO_MAX"/>); por debajo de andar, más despacio
    /// (hasta <see cref="PASO_MIN"/>). No toca UnitAnimatorController: escribe sus propios parámetros, solo si el Animator
    /// los tiene.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class PasoReal : MonoBehaviour
    {
        /// <summary>Velocidades de autor de nuestros clips (m/s), medidas en Blender.</summary>
        public const float ANDAR = 1.15f, CORRER = 3.2f;
        public const float PASO_MIN = 0.7f, PASO_MAX = 1.6f;
        public const string VELOCIDAD = "VelocidadReal", PASO = "Paso";
        static readonly int Velocidad = Animator.StringToHash(VELOCIDAD), Paso = Animator.StringToHash(PASO);
        const float SUAVIZADO = 0.12f;           // s: la velocidad se suaviza (los visuales siguen a la entidad a saltos)

        Animator _animator;
        Vector3 _antes;
        float _v;
        bool _tiene;

        /// <summary>El multiplicador de reproducción para una velocidad real.</summary>
        public static float Multiplicador(float v)
        {
            if (v > CORRER) return Mathf.Min(PASO_MAX, v / CORRER);
            if (v > 0.05f && v < ANDAR) return Mathf.Max(PASO_MIN, v / ANDAR);
            return 1f;
        }

        void OnEnable()
        {
            _animator = GetComponent<Animator>(); _antes = transform.position; _v = 0f;
            _tiene = false;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (var p in _animator.parameters) if (p.nameHash == Velocidad) _tiene = true;
        }

        void LateUpdate()
        {
            if (!_tiene || Time.deltaTime <= 0f) return;
            var d = transform.position - _antes; d.y = 0f; _antes = transform.position;
            float v = d.magnitude / Time.deltaTime;
            _v = Mathf.Lerp(_v, v, 1f - Mathf.Exp(-Time.deltaTime / SUAVIZADO));
            _animator.SetFloat(Velocidad, _v);
            _animator.SetFloat(Paso, Multiplicador(_v));
        }
    }
}
