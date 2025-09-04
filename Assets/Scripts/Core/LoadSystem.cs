using System;
using UnityEngine;

using Core.Persistence;

/// <summary>
/// Handles loading of <see cref="PlayerData"/> from persistent storage.
/// </summary>
public static class LoadSystem
{
    private static ISaveProvider _provider = new LocalSaveProvider();

    /// <summary>
    /// Permite cambiar el proveedor de carga (ej: para CloudSaveProvider en el futuro).
    /// </summary>
    public static void SetProvider(ISaveProvider provider)
    {
        _provider = provider ?? new LocalSaveProvider();
    }

    /// <summary>Carga los datos del jugador usando el proveedor actual y recalcula atributos cacheados.</summary>
    public static PlayerData LoadPlayer()
    {
        try
        {
            var data = _provider.Load();
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load player data: {e}");
            return null;
        }
    }

    public static HeroData LoadHeroForTesting()
    {
        var playerData = LoadPlayer();
        if (playerData == null)
        {
            Debug.LogError("No player data available to load hero.");
            return null;
        }
        var hero = playerData.heroes[0]; // Cargar el primer héroe para pruebas
        if (hero == null)
        {
            Debug.LogError("No hero data found in player data.");
            return null;
        }
        return hero;
    }
}
