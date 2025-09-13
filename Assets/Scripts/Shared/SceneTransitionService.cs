using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneTransitionService
{
    private static readonly Dictionary<string, GamePhase> SceneToPhaseMap = new()
    {
        { "LoginScene", GamePhase.Login },
        { "CharacterSelecctionScene", GamePhase.Login }, // Asumiendo que es parte de login
        { "AvatarCreator", GamePhase.Login },
        { "FeudoScene", GamePhase.Feudo },
        { "BattlePrepScene", GamePhase.Preparacion },
        { "BattleScene", GamePhase.Combate },
        { "PostBattleScene", GamePhase.PostPartida }
    };

    public static void LoadScene(string sceneName)
    {
        // Actualizar GamePhase antes de cargar la escena
        if (SceneToPhaseMap.TryGetValue(sceneName, out var newPhase)) UpdateGamePhase(newPhase);
        else
        {
            Debug.LogWarning($"No se encontró mapeo para la escena {sceneName}. Usando GamePhase.Login por defecto.");
            UpdateGamePhase(GamePhase.Login);
        }

        SceneManager.LoadScene(sceneName);
    }

    public static void LoadSceneAsync(string sceneName)
    {
        if (SceneToPhaseMap.TryGetValue(sceneName, out var newPhase)) UpdateGamePhase(newPhase);
        else
        {
            Debug.LogWarning($"No se encontró mapeo para la escena {sceneName}. Usando GamePhase.Login por defecto.");
            UpdateGamePhase(GamePhase.Login);
        }

        SceneManager.LoadSceneAsync(sceneName);
    }

    public static void UpdateGamePhase(string sceneName)
    {
        if (SceneToPhaseMap.TryGetValue(sceneName, out var newPhase)) UpdateGamePhase(newPhase);
        else
        {
            Debug.LogWarning($"No se encontró mapeo para la escena {sceneName}. Usando GamePhase.Login por defecto.");
            UpdateGamePhase(GamePhase.Login);
        }
    }

    private static void UpdateGamePhase(GamePhase newPhase)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (!query.IsEmpty)
        {
            var entity = query.GetSingletonEntity();
            var gameState = em.GetComponentData<GameStateComponent>(entity);
            gameState.currentPhase = newPhase;
            em.SetComponentData(entity, gameState);
        }
        else
        {
            Debug.LogError("GameStateComponent no encontrado. Asegúrate de que GameStateSystem esté inicializado.");
        }
    }
}