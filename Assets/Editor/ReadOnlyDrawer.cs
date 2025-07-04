using UnityEngine;
using UnityEditor;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Drawer for the ReadOnly attribute.
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Guardar estado anterior del GUI y deshabilitar
            GUI.enabled = false;
            
            // Dibujar la propiedad como readonly
            EditorGUI.PropertyField(position, property, label, true);
            
            // Restaurar estado del GUI
            GUI.enabled = true;
        }
    }
}
