using System;

/// <summary>
/// Eventos del sistema de inventario para notificar cambios a la UI y otros sistemas.
/// </summary>
public static class InventoryEvents
{
    /// <summary>
    /// Se dispara cuando el inventario del héroe cambia (agregar, remover, equipar).
    /// </summary>
    public static Action OnInventoryChanged;

    /// <summary>
    /// Se dispara cuando se equipa un ítem. Parámetro: itemId equipado.
    /// </summary>
    public static Action<string> OnItemEquipped;

    /// <summary>
    /// Se dispara cuando se desequipa un ítem. Parámetro: itemId desequipado.
    /// </summary>
    public static Action<string> OnItemUnequipped;

    /// <summary>
    /// Se dispara cuando se agrega un ítem al inventario. Parámetros: itemId, cantidad.
    /// </summary>
    public static Action<string, int> OnItemAdded;

    /// <summary>
    /// Se dispara cuando se remueve un ítem del inventario. Parámetros: itemId, cantidad.
    /// </summary>
    public static Action<string, int> OnItemRemoved;

    /// <summary>
    /// Se dispara cuando el inventario alcanza el límite de capacidad.
    /// </summary>
    public static Action OnInventoryFull;

    /// <summary>
    /// Se dispara cuando se ordena el inventario.
    /// </summary>
    public static Action OnInventorySorted;

    /// <summary>
    /// Se dispara cuando se ejecuta un efecto de ítem. Parámetros: effectId, hero afectado.
    /// </summary>
    public static Action<string, HeroData> OnItemEffectExecuted;

    /// <summary>
    /// Se dispara cuando se usa un ítem consumible. Parámetros: itemId, hero que lo usa.
    /// </summary>
    public static Action<string, HeroData> OnItemUsed;

    /// <summary>
    /// Se dispara cuando se genera una nueva instancia de equipment. Parámetros: instanceId, itemId.
    /// </summary>
    public static Action<string, string> OnEquipmentInstanceGenerated;

    /// <summary>
    /// Limpia todos los listeners de eventos. Útil para evitar referencias colgantes.
    /// </summary>
    public static void ClearAllListeners()
    {
        OnInventoryChanged = null;
        OnItemEquipped = null;
        OnItemUnequipped = null;
        OnItemAdded = null;
        OnItemRemoved = null;
        OnInventoryFull = null;
        OnInventorySorted = null;
        OnItemEffectExecuted = null;
        OnItemUsed = null;
        OnEquipmentInstanceGenerated = null;
    }
}
