using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estructura serializable para representar un stat individual.
/// </summary>
[Serializable]
public struct SerializableStat
{
    public string name;
    public float value;

    public SerializableStat(string name, float value)
    {
        this.name = name;
        this.value = value;
    }
}

/// <summary>
/// Serializable representation of an item stored in a hero's inventory.
/// Uses an identifier to resolve the actual ScriptableObject definition.
/// Supports both stackable items and unique equipment instances.
/// </summary>
[Serializable]
public class InventoryItem
{
    /// <summary>Identifier of the item definition.</summary>
    public string itemId = string.Empty;

    /// <summary>Type of item for fast filtering.</summary>
    public ItemType itemType = ItemType.None;

    /// <summary>Amount of this item owned. Non-stackable items should use 1.</summary>
    public int quantity = 1;

    /// <summary>
    /// Precio calculado de este ítem. Para stackables es precio por unidad.
    /// Se calcula al crear la instancia usando ItemPricingService.
    /// </summary>
    public int price = 0;

    /// <summary>
    /// Index of the slot in the inventory grid. Use -1 for items not assigned to a slot.
    /// </summary>
    public int slotIndex = -1;

    /// <summary>
    /// Unique identifier for equipment instances. Null for stackable items.
    /// </summary>
    public string instanceId = null;

    /// <summary>
    /// Generated stats for equipment instances serialized as array.
    /// Use GetStat/SetStat methods to access as dictionary.
    /// </summary>
    [SerializeField]
    private SerializableStat[] serializedStats = new SerializableStat[0];

    /// <summary>
    /// Cache del diccionario de stats para acceso rápido en runtime.
    /// Se reconstruye automáticamente desde serializedStats.
    /// </summary>
    [NonSerialized]
    private Dictionary<string, float> _statsCache;

    /// <summary>
    /// Propiedad para acceder a los stats como diccionario.
    /// Reconstruye el cache automáticamente si es necesario.
    /// </summary>
    public Dictionary<string, float> GeneratedStats
    {
        get
        {
            if (_statsCache == null)
            {
                RebuildStatsCache();
            }
            return _statsCache;
        }
        set
        {
            _statsCache = value;
            SerializeStatsFromCache();
        }
    }

    /// <summary>
    /// Verifica si este ítem es una instancia única de equipment.
    /// </summary>
    public bool IsEquipment => !string.IsNullOrEmpty(instanceId);

    /// <summary>
    /// Verifica si este ítem es stackeable.
    /// </summary>
    public bool IsStackable => string.IsNullOrEmpty(instanceId);

    /// <summary>
    /// Obtiene el valor de un stat específico.
    /// </summary>
    /// <param name="statName">Nombre del stat</param>
    /// <returns>Valor del stat o 0 si no existe</returns>
    public float GetStat(string statName)
    {
        return GeneratedStats.GetValueOrDefault(statName, 0f);
    }

    /// <summary>
    /// Establece el valor de un stat específico.
    /// </summary>
    /// <param name="statName">Nombre del stat</param>
    /// <param name="value">Valor del stat</param>
    public void SetStat(string statName, float value)
    {
        GeneratedStats[statName] = value;
        SerializeStatsFromCache();
    }

    /// <summary>
    /// Verifica si tiene un stat específico.
    /// </summary>
    /// <param name="statName">Nombre del stat</param>
    /// <returns>True si el stat existe</returns>
    public bool HasStat(string statName)
    {
        return GeneratedStats.ContainsKey(statName);
    }

    /// <summary>
    /// Obtiene todos los nombres de stats que tiene este ítem.
    /// </summary>
    /// <returns>Lista de nombres de stats</returns>
    public List<string> GetStatNames()
    {
        return new List<string>(GeneratedStats.Keys);
    }

    /// <summary>
    /// Reconstruye el cache de stats desde el array serializado.
    /// </summary>
    private void RebuildStatsCache()
    {
        _statsCache = new Dictionary<string, float>();
        if (serializedStats != null)
        {
            foreach (var stat in serializedStats)
            {
                if (!string.IsNullOrEmpty(stat.name))
                {
                    _statsCache[stat.name] = stat.value;
                }
            }
        }
    }

    /// <summary>
    /// Serializa el cache de stats al array para persistencia.
    /// </summary>
    private void SerializeStatsFromCache()
    {
        if (_statsCache == null)
        {
            serializedStats = new SerializableStat[0];
            return;
        }

        serializedStats = new SerializableStat[_statsCache.Count];
        int index = 0;
        foreach (var kvp in _statsCache)
        {
            serializedStats[index] = new SerializableStat(kvp.Key, kvp.Value);
            index++;
        }
    }
}