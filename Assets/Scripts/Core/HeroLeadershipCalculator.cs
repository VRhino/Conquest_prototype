using UnityEngine;
using Data.Items;

/// <summary>
/// Calculadora especializada para el sistema de liderazgo del héroe.
/// El liderazgo funciona diferente a otros stats: base fija de 700 + stats de armaduras.
/// Este valor determina la capacidad total para llevar squads al campo de batalla.
/// </summary>
public static class HeroLeadershipCalculator
{
    /// <summary>
    /// Valor base de liderazgo para todos los héroes (según especificación del usuario).
    /// </summary>
    public const float BASE_LEADERSHIP = 700f;

    /// <summary>
    /// Calcula el liderazgo total del héroe: base 700 + stats de armaduras equipadas.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <returns>Liderazgo total (base + equipment stats)</returns>
    public static float CalculateLeadership(HeroData heroData)
    {
        if (heroData?.equipment == null)
        {
            Debug.LogWarning("[HeroLeadershipCalculator] HeroData o equipment es null, usando valor base.");
            return BASE_LEADERSHIP;
        }

        float totalLeadership = BASE_LEADERSHIP;

        // Sumar liderazgo de todas las piezas de armadura equipadas
        totalLeadership += GetLeadershipFromEquipmentSlot(heroData.equipment.helmet);
        totalLeadership += GetLeadershipFromEquipmentSlot(heroData.equipment.torso);
        totalLeadership += GetLeadershipFromEquipmentSlot(heroData.equipment.gloves);
        totalLeadership += GetLeadershipFromEquipmentSlot(heroData.equipment.pants);
        totalLeadership += GetLeadershipFromEquipmentSlot(heroData.equipment.boots);

        // Nota: Las armas NO proporcionan liderazgo según especificación del usuario

        return totalLeadership;
    }

    /// <summary>
    /// Extrae el valor de liderazgo de un item de equipamiento específico.
    /// </summary>
    /// <param name="inventoryItem">Item de inventario equipado (helmet, torso, etc.)</param>
    /// <returns>Valor de liderazgo del item equipado, o 0 si no hay item o no tiene liderazgo</returns>
    private static float GetLeadershipFromEquipmentSlot(InventoryItem inventoryItem)
    {
        if (inventoryItem?.GeneratedStats == null)
            return 0f;

        // Buscar stat de liderazgo en los stats del item
        var stats = inventoryItem.GeneratedStats;
        
        if (stats.ContainsKey("liderazgo"))
            return stats["liderazgo"];
            
        if (stats.ContainsKey("leadership"))
            return stats["leadership"];

        return 0f;
    }
}
