using UnityEngine;

namespace BronzeAge.Demo
{
    /// <summary>Rótulo flotante que mira siempre a la cámara.</summary>
    public class RotuloHaciaCamara : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c != null) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position);
        }
    }
}
