using System.Linq;
using UnityEngine;
using ConquestTactics.Dialogue;

namespace ConquestTactics.Dialogue
{
    /// <summary>
    /// Sistema encargado de ejecutar los efectos de diálogo.
    /// Maneja la validación, ejecución y notificación de efectos.
    /// </summary>
    public static class DialogueEffectSystem
{
    /// <summary>
    /// Ejecuta una lista de efectos de diálogo por sus IDs en orden de prioridad.
    /// </summary>
    /// <param name="effectIds">Array de IDs de efectos a ejecutar</param>
    /// <param name="hero">Héroe sobre el que aplicar los efectos</param>
    /// <param name="npcId">ID del NPC que ejecuta los efectos</param>
    /// <param name="parameters">Parámetros adicionales</param>
    /// <returns>True si todos los efectos se ejecutaron exitosamente</returns>
    public static bool ExecuteDialogueEffects(string[] effectIds, HeroData hero, string npcId = null, DialogueParameters parameters = null)
    {
        if (effectIds == null || effectIds.Length == 0)
        {
            LogInfo("No dialogue effect IDs to execute");
            return true;
        }

        // Resolver efectos por ID
        var effects = DialogueEffectDatabase.GetEffects(effectIds);
        return ExecuteDialogueEffects(effects, hero, npcId, parameters);
    }

    /// <summary>
    /// Ejecuta una lista de efectos de diálogo en orden de prioridad.
    /// </summary>
    /// <param name="effects">Lista de efectos a ejecutar</param>
    /// <param name="hero">Héroe sobre el que aplicar los efectos</param>
    /// <param name="npcId">ID del NPC que ejecuta los efectos</param>
    /// <param name="parameters">Parámetros adicionales</param>
    /// <returns>True si todos los efectos se ejecutaron exitosamente</returns>
    public static bool ExecuteDialogueEffects(DialogueEffect[] effects, HeroData hero, string npcId = null, DialogueParameters parameters = null)
    {
        if (effects == null || effects.Length == 0)
        {
            LogInfo("No dialogue effects to execute");
            return true;
        }

        if (hero == null)
        {
            LogError("Cannot execute dialogue effects on null hero");
            return false;
        }

        // Verificar si todos los efectos se pueden ejecutar
        foreach (var effect in effects)
        {
            if (effect == null)
            {
                LogWarning("Null dialogue effect found, skipping");
                continue;
            }

            if (!effect.CanExecute(hero, npcId))
            {
                LogWarning($"Dialogue effect {effect.DisplayName} cannot be executed");
                return false;
            }
        }

        // Ordenar efectos por prioridad (mayor prioridad primero)
        var sortedEffects = effects
            .Where(e => e != null)
            .OrderByDescending(e => e.GetExecutionPriority())
            .ToArray();

        // Ejecutar todos los efectos
        bool allSucceeded = true;
        foreach (var effect in sortedEffects)
        {
            try
            {
                bool success = effect.Execute(hero, npcId, parameters);
                if (!success)
                {
                    LogError($"Failed to execute dialogue effect: {effect.DisplayName}");
                    allSucceeded = false;
                    break;
                }
                else
                {
                    LogInfo($"Successfully executed dialogue effect: {effect.DisplayName}");
                }
            }
            catch (System.Exception e)
            {
                LogError($"Exception executing dialogue effect {effect.DisplayName}: {e.Message}");
                allSucceeded = false;
                break;
            }
        }

        if (allSucceeded)
        {
            LogInfo($"All dialogue effects executed successfully for {hero.heroName}");
        }

        return allSucceeded;
    }
    
    /// <summary>
    /// Obtiene una descripción completa de efectos de diálogo por IDs.
    /// </summary>
    /// <param name="effectIds">Array de IDs de efectos</param>
    /// <returns>Descripción textual de los efectos</returns>
    public static string GetDialogueEffectsPreview(string[] effectIds)
    {
        if (effectIds == null || effectIds.Length == 0)
        {
            return "No effects";
        }

        var effects = DialogueEffectDatabase.GetEffects(effectIds);
        return GetDialogueEffectsPreview(effects);
    }

    /// <summary>
    /// Obtiene una descripción completa de todos los efectos de diálogo.
    /// </summary>
    /// <param name="effects">Lista de efectos</param>
    /// <returns>Descripción textual de los efectos</returns>
    public static string GetDialogueEffectsPreview(DialogueEffect[] effects)
    {
        if (effects == null || effects.Length == 0)
        {
            return "No effects";
        }

        var validEffects = effects.Where(e => e != null).ToArray();
        if (validEffects.Length == 0)
        {
            return "No valid effects";
        }

        if (validEffects.Length == 1)
        {
            return validEffects[0].GetPreviewText();
        }

        string preview = "Multiple effects:\n";
        foreach (var effect in validEffects.OrderByDescending(e => e.GetExecutionPriority()))
        {
            preview += $"• {effect.GetPreviewText()}\n";
        }

        return preview.TrimEnd('\n');
    }

    /// <summary>
    /// Verifica si todos los efectos se pueden ejecutar por IDs.
    /// </summary>
    /// <param name="effectIds">Array de IDs de efectos</param>
    /// <param name="hero">Héroe objetivo</param>
    /// <param name="npcId">ID del NPC</param>
    /// <returns>True si todos los efectos se pueden ejecutar</returns>
    public static bool CanExecuteAllEffects(string[] effectIds, HeroData hero, string npcId = null)
    {
        if (effectIds == null || effectIds.Length == 0)
        {
            return true;
        }

        var effects = DialogueEffectDatabase.GetEffects(effectIds);
        return CanExecuteAllEffects(effects, hero, npcId);
    }

    /// <summary>
    /// Verifica si todos los efectos se pueden ejecutar en el héroe especificado.
    /// </summary>
    /// <param name="effects">Lista de efectos</param>
    /// <param name="hero">Héroe objetivo</param>
    /// <param name="npcId">ID del NPC</param>
    /// <returns>True si todos los efectos se pueden ejecutar</returns>
    public static bool CanExecuteAllEffects(DialogueEffect[] effects, HeroData hero, string npcId = null)
    {
        if (effects == null || effects.Length == 0)
        {
            return true;
        }

        return effects.Where(e => e != null).All(e => e.CanExecute(hero, npcId));
    }

    /// <summary>
    /// Obtiene información detallada de los efectos para debugging.
    /// </summary>
    /// <param name="effects">Lista de efectos</param>
    /// <returns>Información detallada</returns>
    public static string GetEffectsDebugInfo(DialogueEffect[] effects)
    {
        if (effects == null || effects.Length == 0)
        {
            return "No dialogue effects";
        }

        string info = $"Dialogue Effects ({effects.Length}):\n";
        for (int i = 0; i < effects.Length; i++)
        {
            var effect = effects[i];
            if (effect != null)
            {
                info += $"  [{i}] {effect.DisplayName} (Priority: {effect.GetExecutionPriority()})\n";
                info += $"      {effect.Description}\n";
                info += $"      Preview: {effect.GetPreviewText()}\n";
            }
            else
            {
                info += $"  [{i}] NULL EFFECT\n";
            }
        }

        return info;
    }

    #region Logging
    private static void LogInfo(string message)
    {
        Debug.Log($"[DialogueEffectSystem] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[DialogueEffectSystem] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[DialogueEffectSystem] {message}");
    }
    #endregion
}
}
