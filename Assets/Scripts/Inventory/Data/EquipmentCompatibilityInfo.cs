using System;
using System.Collections.Generic;

/// <summary>
/// Estructura de datos que contiene información sobre la compatibilidad de equipamiento
/// y los requerimientos de confirmación para equipar un ítem.
/// </summary>
[Serializable]
public struct EquipmentCompatibilityInfo
{
    /// <summary>
    /// Indica si se requiere confirmación del usuario para equipar el ítem.
    /// </summary>
    public bool RequiresConfirmation;

    /// <summary>
    /// Lista de nombres de piezas incompatibles que serán desequipadas.
    /// </summary>
    public string[] IncompatiblePieceNames;

    /// <summary>
    /// Lista de InventoryItems incompatibles que serán desequipadas.
    /// </summary>
    public List<InventoryItem> IncompatiblePieces;

    /// <summary>
    /// Indica si hay espacio suficiente en el inventario para las piezas desequipadas.
    /// </summary>
    public bool HasInventorySpace;

    /// <summary>
    /// Mensaje de advertencia para mostrar al usuario.
    /// </summary>
    public string WarningMessage;

    /// <summary>
    /// Tipo de conflicto de compatibilidad.
    /// </summary>
    public CompatibilityConflictType ConflictType;

    /// <summary>
    /// Constructor para crear información de compatibilidad.
    /// </summary>
    public EquipmentCompatibilityInfo(
        bool requiresConfirmation,
        List<InventoryItem> incompatiblePieces = null,
        bool hasInventorySpace = true,
        string warningMessage = "",
        CompatibilityConflictType conflictType = CompatibilityConflictType.None)
    {
        RequiresConfirmation = requiresConfirmation;
        IncompatiblePieces = incompatiblePieces ?? new List<InventoryItem>();
        HasInventorySpace = hasInventorySpace;
        WarningMessage = warningMessage;
        ConflictType = conflictType;

        // Generar nombres de piezas incompatibles
        if (IncompatiblePieces != null && IncompatiblePieces.Count > 0)
        {
            IncompatiblePieceNames = new string[IncompatiblePieces.Count];
            for (int i = 0; i < IncompatiblePieces.Count; i++)
            {
                var itemData = InventoryUtils.GetItemData(IncompatiblePieces[i].itemId);
                IncompatiblePieceNames[i] = itemData?.name ?? IncompatiblePieces[i].itemId;
            }
        }
        else
        {
            IncompatiblePieceNames = new string[0];
        }
    }
}

/// <summary>
/// Tipos de conflicto de compatibilidad de equipamiento.
/// </summary>
public enum CompatibilityConflictType
{
    None,
    WeaponIncompatibleWithArmor,
    ArmorIncompatibleWithWeapon
}
