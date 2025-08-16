using UnityEngine;
using Data.Items;

/// <summary>
/// Interface base para todos los componentes internos del tooltip.
/// Define el contrato para inicialización y limpieza de componentes.
/// </summary>
public interface ITooltipComponent
{
    /// <summary>
    /// Inicializa el componente con referencia al controller principal.
    /// </summary>
    /// <param name="controller">Controller principal del tooltip</param>
    void Initialize(InventoryTooltipController controller);
    
    /// <summary>
    /// Limpia recursos y referencias del componente.
    /// </summary>
    void Cleanup();
}
