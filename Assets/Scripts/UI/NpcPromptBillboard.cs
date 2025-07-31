using UnityEngine;

namespace ConquestTactics.UI
{
    /// <summary>
    /// Hace que el prompt 3D siempre mire al héroe local o a la cámara principal.
    /// </summary>
    public class NpcPromptBillboard : MonoBehaviour
    {
        [Tooltip("Si está vacío, usará Camera.main")] 
        public Transform targetToLookAt;
        [Tooltip("Offset vertical sobre el NPC")] 
        public Vector3 worldOffset = new Vector3(0, 2f, 0);

        private void LateUpdate()
        {
            // Si no hay target, busca la cámara principal
            if (targetToLookAt == null && Camera.main != null)
                targetToLookAt = Camera.main.transform;
            if (targetToLookAt == null) return;

            // Aplica offset y mira al target
            transform.position = transform.parent.position + worldOffset;
            transform.LookAt(transform.position + (transform.position - targetToLookAt.position));
        }

        /// <summary>
        /// Permite cambiar el texto del prompt desde código.
        /// </summary>
        public void SetPromptText(string text)
        {
            var tmp = GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }
    }
}
