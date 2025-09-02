using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio estático para gestionar las definiciones de clases de héroe.
/// Proporciona acceso centralizado a las configuraciones de clase.
/// </summary>
public static class HeroClassManager
{
    private static readonly Dictionary<string, HeroClassDefinition> _classCache = new();

    /// <summary>
    /// Obtiene la definición de clase para el classId especificado.
    /// </summary>
    /// <param name="classId">ID de la clase (ej: "SwordAndShield")</param>
    /// <returns>HeroClassDefinition o null si no se encuentra</returns>
    public static HeroClassDefinition GetClassDefinition(string classId)
    {
        if (string.IsNullOrEmpty(classId))
            return null;

        // Buscar en cache primero
        if (_classCache.TryGetValue(classId, out var cached))
            return cached;

        // Cargar desde Resources
        var definition = Resources.Load<HeroClassDefinition>($"Data/HeroClasses/{classId}");
        if (definition != null)
        {
            _classCache[classId] = definition;
            return definition;
        }

        Debug.LogWarning($"[HeroClassManager] No se encontró HeroClassDefinition para classId: {classId}");
        return null;
    }

    /// <summary>
    /// Limpia el cache de definiciones de clase.
    /// Útil cuando se recarga contenido en el editor.
    /// </summary>
    public static void ClearCache()
    {
        _classCache.Clear();
    }
}
