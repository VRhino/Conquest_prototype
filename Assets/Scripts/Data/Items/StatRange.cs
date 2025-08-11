using System;
using UnityEngine;

namespace Data.Items
{
    /// <summary>
    /// Representa un rango de valores para la generación de stats aleatorios.
    /// </summary>
    [Serializable]
    public struct FloatRange
    {
        [SerializeField] public float min;
        [SerializeField] public float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>
        /// Genera un valor aleatorio dentro del rango.
        /// </summary>
        public float GetRandomValue()
        {
            return UnityEngine.Random.Range(min, max);
        }

        /// <summary>
        /// Verifica si el rango es válido (min <= max).
        /// </summary>
        public bool IsValid => min <= max;

        public override string ToString()
        {
            return $"{min:F1}-{max:F1}";
        }
    }

    /// <summary>
    /// Rango para valores enteros.
    /// </summary>
    [Serializable]
    public struct IntRange
    {
        [SerializeField] public int min;
        [SerializeField] public int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>
        /// Genera un valor aleatorio dentro del rango.
        /// </summary>
        public int GetRandomValue()
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        /// <summary>
        /// Verifica si el rango es válido (min <= max).
        /// </summary>
        public bool IsValid => min <= max;

        public override string ToString()
        {
            return $"{min}-{max}";
        }
    }
}
