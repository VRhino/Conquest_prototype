using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de eventos para notificaciones relacionadas con squads.
/// Usado principalmente por HeroSliceController para actualizaciones dinámicas.
/// </summary>
public static class BattlePreparationEvents
{
    #region Events
    
    /// <summary>
    /// Se dispara cuando la lista de squads disponibles de un héroe cambia.
    /// Incluye tanto agregar nuevos squads como remover existentes.
    /// </summary>
    public static event Action<string, List<SquadIconData>> SquadsUpdated;
    
    #endregion
    
    #region Event Triggers
    
    /// <summary>
    /// Dispara el evento de actualización de squads para un héroe específico.
    /// </summary>
    /// <param name="heroId">ID del héroe (puede ser heroName o un identificador único)</param>
    /// <param name="SelectedSquads">Lista actualizada de IDs de squads seleccionados</param>
    public static void TriggerSquadsUpdated(string heroId, List<SquadIconData> selectedSquads)
    {
        if (string.IsNullOrEmpty(heroId))
        {
            Debug.LogWarning("[SquadEvents] Intentando disparar SquadsUpdated con heroId null o vacío");
            return;
        }
        
        if (selectedSquads == null)
        {
            Debug.LogWarning($"[SquadEvents] Intentando disparar SquadsUpdated con availableSquads null para héroe: {heroId}");
            return;
        }
        
        SquadsUpdated?.Invoke(heroId, selectedSquads);
        Debug.Log($"[SquadEvents] SquadsUpdated disparado para héroe: {heroId}, squads count: {selectedSquads.Count}");
    }
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Desuscribe todos los listeners de los eventos.
    /// Útil para cleanup o reset del sistema de eventos.
    /// </summary>
    public static void ClearAllListeners()
    {
        SquadsUpdated = null;
        Debug.Log("[SquadEvents] Todos los listeners han sido limpiados");
    }
    
    /// <summary>
    /// Obtiene el número de listeners suscritos a SquadsUpdated.
    /// Útil para debugging y validación.
    /// </summary>
    /// <returns>Número de listeners suscritos</returns>
    public static int GetSquadsUpdatedListenerCount()
    {
        return SquadsUpdated?.GetInvocationList().Length ?? 0;
    }
    #endregion
}
