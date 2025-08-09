/// <summary>
/// Estadísticas del inventario para análisis y debugging.
/// </summary>
[System.Serializable]
public class InventoryStats
{
    public int TotalItems;
    public int TotalQuantity;
    public int InventoryLimit;
    public float UsagePercentage;
    
    // Contadores por tipo
    public int WeaponCount;
    public int HelmetCount;
    public int TorsoCount;
    public int GlovesCount;
    public int PantsCount;
    public int BootsCount;
    public int ConsumableCount;
    public int VisualCount;
}
