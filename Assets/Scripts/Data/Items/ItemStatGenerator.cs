using System.Collections.Generic;
using UnityEngine;

namespace Data.Items
{
    /// <summary>
    /// Clase base abstracta para todos los generadores de stats de ítems.
    /// Permite crear diferentes tipos de generadores de forma modular.
    /// </summary>
    public abstract class ItemStatGenerator : ScriptableObject
    {
        [Header("Generator Info")]
        [SerializeField] protected string generatorId;
        [SerializeField] protected string displayName;
        [TextArea(2, 4)]
        [SerializeField] protected string description;

        /// <summary>
        /// ID único del generator para referencia.
        /// </summary>
        public string GeneratorId => generatorId;

        /// <summary>
        /// Nombre mostrado en el editor.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Descripción del generator.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Genera un diccionario de stats aleatorios basado en la configuración del generator.
        /// </summary>
        /// <returns>Diccionario con nombre del stat como key y valor como value</returns>
        public abstract Dictionary<string, float> GenerateStats();

        /// <summary>
        /// Obtiene una descripción de los stats que puede generar este generator.
        /// Útil para mostrar en tooltips o interfaces.
        /// </summary>
        /// <returns>String con preview de los stats</returns>
        public abstract string GetStatsPreview();

        /// <summary>
        /// Valida que la configuración del generator sea correcta.
        /// </summary>
        /// <returns>True si la configuración es válida</returns>
        public virtual bool IsValid()
        {
            return !string.IsNullOrEmpty(generatorId) && !string.IsNullOrEmpty(displayName);
        }

        /// <summary>
        /// Obtiene el valor de un stat específico si existe en el generator.
        /// Útil para comparaciones o cálculos específicos.
        /// </summary>
        /// <param name="statName">Nombre del stat</param>
        /// <returns>Valor del stat o 0 si no existe</returns>
        public virtual float GetStatValue(string statName)
        {
            var stats = GenerateStats();
            return stats.GetValueOrDefault(statName, 0f);
        }

        /// <summary>
        /// Obtiene todos los nombres de stats que puede generar este generator.
        /// </summary>
        /// <returns>Lista con nombres de stats</returns>
        public abstract List<string> GetStatNames();

        /// <summary>
        /// Método virtual para realizar validaciones adicionales específicas del tipo de generator.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(generatorId))
            {
                generatorId = name;
            }
        }
    }
}
