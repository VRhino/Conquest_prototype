using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneTransitionService
{
    public static class SceneNames
    {
        public const string Login = "LoginScene";
        public const string HeroSelection = "HeroSelectionScene";
        public const string AvatarCreator = "AvatarCreator";
        public const string CharacterSelection = "CharacterSelecctionScene";
        public const string Feudo = "FeudoScene";
        public const string BattlePrep = "BattlePrepScene";
        public const string Battle = "BattleScene";
        public const string PostBattle = "PostBattleScene";
    }

    private static readonly Dictionary<string, GamePhase> SceneToPhaseMap = new()
    {
        { SceneNames.Login, GamePhase.Login },
        { SceneNames.HeroSelection, GamePhase.CharacterSelection },
        { SceneNames.CharacterSelection, GamePhase.CharacterSelection },
        { SceneNames.AvatarCreator, GamePhase.AvatarCreation },
        { SceneNames.Feudo, GamePhase.Feudo },
        { SceneNames.BattlePrep, GamePhase.BattlePreparation },
        { SceneNames.Battle, GamePhase.Combate },
        { SceneNames.PostBattle, GamePhase.PostPartida }
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

    public static void UpdateGamePhase(GamePhase newPhase)
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