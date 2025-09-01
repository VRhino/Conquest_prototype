using System;

/// <summary>
/// Estructura que contiene el resultado de una transacción de tienda (compra o venta).
/// Proporciona información detallada sobre el éxito o fallo de la operación.
/// </summary>
[Serializable]
public struct TransactionResult
{
    /// <summary>
    /// Indica si la transacción fue exitosa.
    /// </summary>
    public bool Success;

    /// <summary>
    /// Mensaje de error detallado si la transacción falló.
    /// </summary>
    public string ErrorMessage;

    /// <summary>
    /// Tipo específico de error que ocurrió durante la transacción.
    /// </summary>
    public TransactionErrorType ErrorType;

    /// <summary>
    /// Cantidad de Bronze requerida para la transacción.
    /// </summary>
    public int RequiredBronze;

    /// <summary>
    /// Cantidad de Bronze disponible en el momento de la transacción.
    /// </summary>
    public int AvailableBronze;

    /// <summary>
    /// Constructor para crear un resultado exitoso.
    /// </summary>
    /// <param name="requiredBronze">Bronze usado en la transacción</param>
    /// <param name="availableBronze">Bronze disponible después de la transacción</param>
    /// <returns>TransactionResult exitoso</returns>
    public static TransactionResult CreateSuccess(int requiredBronze = 0, int availableBronze = 0)
    {
        return new TransactionResult
        {
            Success = true,
            ErrorMessage = string.Empty,
            ErrorType = TransactionErrorType.None,
            RequiredBronze = requiredBronze,
            AvailableBronze = availableBronze
        };
    }

    /// <summary>
    /// Constructor para crear un resultado de fallo.
    /// </summary>
    /// <param name="errorType">Tipo de error</param>
    /// <param name="errorMessage">Mensaje descriptivo del error</param>
    /// <param name="requiredBronze">Bronze requerido</param>
    /// <param name="availableBronze">Bronze disponible</param>
    /// <returns>TransactionResult con error</returns>
    public static TransactionResult CreateFailure(TransactionErrorType errorType, string errorMessage, 
        int requiredBronze = 0, int availableBronze = 0)
    {
        return new TransactionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorType = errorType,
            RequiredBronze = requiredBronze,
            AvailableBronze = availableBronze
        };
    }
}

/// <summary>
/// Tipos de error que pueden ocurrir durante las transacciones de tienda.
/// </summary>
public enum TransactionErrorType
{
    /// <summary>Sin error - transacción exitosa.</summary>
    None,
    
    /// <summary>No hay suficiente Bronze para completar la compra.</summary>
    InsufficientBronze,
    
    /// <summary>No hay espacio suficiente en el inventario.</summary>
    InsufficientSpace,
    
    /// <summary>El ítem no es válido o no se puede vender.</summary>
    InvalidItem,
    
    /// <summary>Error general durante la ejecución de la transacción.</summary>
    TransactionFailed
}
