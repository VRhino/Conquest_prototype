using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneTransitionService
{
    public static class SceneNames
    {
        public const string WorldMap = "WorldMapCampaign";
        public const string SettlementPreview = "SettlementPreview";
        public const string Settlement = "SettlementScene";
        public const string Login = "LoginScene";
        public const string HeroSelection = "HeroSelectionScene";
        public const string AvatarCreator = "AvatarCreator";
        public const string CharacterSelection = "CharacterSelecctionScene";
        public const string Feudo = "FeudoScene";
        public const string BattlePrep = "BattlePrepScene";
        public const string Battle = "BattleScene";
        public const string PostBattle = "PostBattleScene";
    }

    private static readonly Dictionary<string, GamePhase> SceneToPhaseMap = new(System.StringComparer.OrdinalIgnoreCase)
    {
        { SceneNames.WorldMap, GamePhase.WorldMap },
        { SceneNames.SettlementPreview, GamePhase.Feudo },
        { SceneNames.Settlement, GamePhase.Feudo },
        { SceneNames.Login, GamePhase.Login },
        { SceneNames.HeroSelection, GamePhase.CharacterSelection },
        { SceneNames.CharacterSelection, GamePhase.CharacterSelection },
        { SceneNames.AvatarCreator, GamePhase.AvatarCreation },
        { SceneNames.Feudo, GamePhase.Feudo },
        { SceneNames.BattlePrep, GamePhase.BattlePreparation },
        { SceneNames.Battle, GamePhase.Combate },
        { SceneNames.PostBattle, GamePhase.PostPartida }
    };

    /// <summary>Interface scenes always own a free pointer, even when a nested panel closes.</summary>
    public static bool RequiresFreeCursor
    {
        get
        {
            if (!SceneToPhaseMap.TryGetValue(SceneManager.GetActiveScene().name, out var phase)) return false;
            return phase == GamePhase.Login || phase == GamePhase.CharacterSelection
                || phase == GamePhase.AvatarCreation || phase == GamePhase.BattlePreparation
                || phase == GamePhase.PostPartida || phase == GamePhase.WorldMap;
        }
    }

    /// <summary>Equipment is frozen for the complete combat phase, including respawn.</summary>
    public static bool IsBattleEquipmentLocked
    {
        get
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
                if (query.CalculateEntityCount() == 1)
                    return query.GetSingleton<GameStateComponent>().currentPhase == GamePhase.Combate;
            }
            return SceneManager.GetActiveScene().name == SceneNames.Battle;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterSceneCursorPolicy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Scene unload callbacks may restore gameplay lock; apply the destination policy afterwards.
        if (RequiresFreeCursor) ApplyCursorState(true);
    }

    public static bool ApplyCursorState(bool uiRequested)
    {
        bool free = uiRequested || RequiresFreeCursor;
        var mode = free ? CursorLockMode.None : CursorLockMode.Locked;
        if (Cursor.lockState != mode) Cursor.lockState = mode;
        if (Cursor.visible != free) Cursor.visible = free;
        return free;
    }

    public static void LoadScene(string sceneName)
    {
        UpdateGamePhase(sceneName);
        SceneManager.LoadScene(sceneName);
    }

    public static void LoadSceneAsync(string sceneName)
    {
        UpdateGamePhase(sceneName);
        SceneManager.LoadSceneAsync(sceneName);
    }

    public static void UpdateGamePhase(string sceneName)
    {
        string sceneKey = System.IO.Path.GetFileNameWithoutExtension(sceneName);
        if (SceneToPhaseMap.TryGetValue(sceneKey, out var newPhase)) UpdateGamePhase(newPhase);
        else
        {
            Debug.LogWarning($"No se encontró mapeo para la escena {sceneName}. Usando GamePhase.Login por defecto.");
            UpdateGamePhase(GamePhase.Login);
        }
    }

    public static void UpdateGamePhase(GamePhase newPhase)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        var em = world.EntityManager;
        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
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
