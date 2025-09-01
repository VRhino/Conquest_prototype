using UnityEngine;
using Data.Items;

/// <summary>
/// Clase base abstracta para todas las configuraciones de precios de ítems.
/// Permite crear diferentes estrategias de pricing de forma modular.
/// </summary>
public abstract class PricingConfigurationSO : ScriptableObject
    {
        [Header("Configuration Info")]
        [SerializeField] protected string configId;
        [SerializeField] protected string displayName;
        [TextArea(2, 4)]
        [SerializeField] protected string description;

        /// <summary>
        /// ID único de la configuración para referencia.
        /// </summary>
        public string ConfigId => configId;

        /// <summary>
        /// Nombre mostrado en el editor.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Descripción de la configuración.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Calcula el precio para un ítem específico.
        /// </summary>
        /// <param name="itemData">Datos del prototipo del ítem</param>
        /// <param name="inventoryItem">Instancia específica del ítem (opcional)</param>
        /// <returns>Precio calculado en monedas</returns>
        public abstract int CalculatePrice(ItemData itemData, InventoryItem inventoryItem = null);

        /// <summary>
        /// Obtiene una vista previa de cómo funciona esta configuración de precios.
        /// Útil para mostrar en el editor o debugging.
        /// </summary>
        /// <returns>String descriptivo del comportamiento de pricing</returns>
        public virtual string GetPricePreview()
        {
            return $"{displayName}: {description}";
        }

        /// <summary>
        /// Valida que la configuración sea correcta y funcional.
        /// </summary>
        /// <returns>True si la configuración es válida</returns>
        public virtual bool IsValid()
        {
            return !string.IsNullOrEmpty(configId) && !string.IsNullOrEmpty(displayName);
        }
    }
