using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace ConquestTactics.Dialogue
{
    /// <summary>
    /// Base de datos centralizada para efectos de diálogo.
    /// Maneja la carga y resolución de efectos por ID.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueEffectDatabase", menuName = "Dialogue/Dialogue Effect Database", order = 0)]
    public class DialogueEffectDatabase : ScriptableObject
    {
        [Header("Dialogue Effects")]
        [SerializeField] private DialogueEffect[] dialogueEffects;

        private Dictionary<string, DialogueEffect> _effectsById;
        private static DialogueEffectDatabase _instance;

        /// <summary>
        /// Instancia singleton de la base de datos.
        /// </summary>
        public static DialogueEffectDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<DialogueEffectDatabase>("DialogueEffectDatabase");
                    if (_instance == null)
                    {
                        Debug.LogError("DialogueEffectDatabase not found in Resources folder!");
                    }
                    else
                    {
                        _instance.Initialize();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Inicializa la base de datos construyendo el diccionario de efectos.
        /// </summary>
        private void Initialize()
        {
            _effectsById = new Dictionary<string, DialogueEffect>();
            
            if (dialogueEffects != null)
            {
                foreach (var effect in dialogueEffects)
                {
                    if (effect != null && !string.IsNullOrEmpty(effect.EffectId))
                    {
                        if (_effectsById.ContainsKey(effect.EffectId))
                        {
                            Debug.LogWarning($"Duplicate dialogue effect ID: {effect.EffectId}");
                        }
                        else
                        {
                            _effectsById[effect.EffectId] = effect;
                        }
                    }
                }
            }

            Debug.Log($"DialogueEffectDatabase initialized with {_effectsById.Count} effects");
        }

        /// <summary>
        /// Obtiene un efecto de diálogo por su ID.
        /// </summary>
        /// <param name="effectId">ID del efecto</param>
        /// <returns>El efecto o null si no se encuentra</returns>
        public static DialogueEffect GetEffect(string effectId)
        {
            if (string.IsNullOrEmpty(effectId))
            {
                return null;
            }

            var database = Instance;
            if (database?._effectsById != null && database._effectsById.TryGetValue(effectId, out var effect))
            {
                return effect;
            }

            Debug.LogWarning($"Dialogue effect not found: {effectId}");
            return null;
        }

        /// <summary>
        /// Obtiene múltiples efectos por sus IDs.
        /// </summary>
        /// <param name="effectIds">Array de IDs de efectos</param>
        /// <returns>Array de efectos (puede contener nulls para IDs no encontrados)</returns>
        public static DialogueEffect[] GetEffects(string[] effectIds)
        {
            if (effectIds == null || effectIds.Length == 0)
            {
                return new DialogueEffect[0];
            }

            return effectIds.Select(GetEffect).Where(e => e != null).ToArray();
        }

        /// <summary>
        /// Verifica si un efecto existe en la base de datos.
        /// </summary>
        /// <param name="effectId">ID del efecto</param>
        /// <returns>True si el efecto existe</returns>
        public static bool HasEffect(string effectId)
        {
            if (string.IsNullOrEmpty(effectId))
            {
                return false;
            }

            var database = Instance;
            return database?._effectsById?.ContainsKey(effectId) ?? false;
        }

        /// <summary>
        /// Obtiene todos los IDs de efectos disponibles.
        /// </summary>
        /// <returns>Array de IDs de efectos</returns>
        public static string[] GetAllEffectIds()
        {
            var database = Instance;
            return database?._effectsById?.Keys.ToArray() ?? new string[0];
        }

        /// <summary>
        /// Obtiene información de debugging sobre la base de datos.
        /// </summary>
        /// <returns>String con información de debugging</returns>
        public static string GetDebugInfo()
        {
            var database = Instance;
            if (database?._effectsById == null)
            {
                return "DialogueEffectDatabase not initialized";
            }

            string info = $"DialogueEffectDatabase ({database._effectsById.Count} effects):\n";
            foreach (var kvp in database._effectsById)
            {
                info += $"  {kvp.Key}: {kvp.Value.DisplayName}\n";
            }

            return info;
        }

        #region Editor Utilities
        [ContextMenu("Rebuild Database")]
        private void RebuildDatabase()
        {
            Initialize();
        }

        [ContextMenu("Log Debug Info")]
        private void LogDebugInfo()
        {
            Debug.Log(GetDebugInfo());
        }

        private void OnValidate()
        {
            // Verificar IDs duplicados en el editor
            if (dialogueEffects != null)
            {
                var ids = new HashSet<string>();
                foreach (var effect in dialogueEffects)
                {
                    if (effect != null && !string.IsNullOrEmpty(effect.EffectId))
                    {
                        if (!ids.Add(effect.EffectId))
                        {
                            Debug.LogWarning($"Duplicate dialogue effect ID found: {effect.EffectId}", this);
                        }
                    }
                }
            }
        }
        #endregion
    }
}
