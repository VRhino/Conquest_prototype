using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Controlador principal de la escena de batalla.
/// Recibe BattleData desde BattleTransitionData y coordina la inicialización de la batalla.
/// </summary>
public class BattleSceneController : MonoBehaviour
{
    #region Private Fields

    private BattleData _currentBattleData;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // 1. Obtener datos de transición
        _currentBattleData = BattleTransitionData.Instance.GetAndClearBattleData();

        if (_currentBattleData != null)
        {
            Debug.Log($"[BattleSceneController] BattleData recibido - battleID: {_currentBattleData.battleID}");
            InitializeBattle();
        }
        else
        {
            // [TESTING ONLY] Setup test environment if available
            TestEnvironmentInitializer testEnv = FindAnyObjectByType<TestEnvironmentInitializer>();
            if (testEnv != null)
            {
                testEnv.SetupTestEnvironment();
                _currentBattleData = testEnv.GenerateBattleData(PlayerSessionService.SelectedHero);
                testEnv.SetGamePhase(GamePhase.Combate);
                Debug.Log($"[BattleSceneController] BattleData de test generado - battleID: {_currentBattleData.battleID}");
                InitializeBattle();
            }
            else
            {
                Debug.LogError("[BattleSceneController] No hay BattleData disponible y no hay TestEnvironmentInitializer");
            }
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Inicializa la batalla con los datos recibidos.
    /// </summary>
    private void InitializeBattle()
    {
        LogBattleData();

        // Sincronizar info de batalla hacia ECS antes de que HeroSpawnSystem actúe
        SyncBattleDataToECS();

        // [TODO] Aquí se inicializarán los sistemas de batalla:
        // - Spawning de unidades
        // - Carga del mapa de batalla
        // - HUD de combate
        // - Timer de batalla
    }

    /// <summary>
    /// Sincroniza la información del héroe y la batalla (lado, spawn point) 
    /// desde POCOs a los singletons de ECS (DataContainerComponent).
    /// </summary>
    private void SyncBattleDataToECS()
    {
        var localHeroName = PlayerSessionService.SelectedHero?.heroName;
        if (string.IsNullOrEmpty(localHeroName)) return;

        BattleHeroData localHero = _currentBattleData.findHeroDataByName(localHeroName);
        if (localHero == null)
        {
            Debug.LogWarning($"[BattleSceneController] No se encontró al héroe local '{localHeroName}' para sincronizar con ECS.");
            return;
        }

        // Parsear el string ID a int extrayendo la parte numérica (ej: "at1" → 1, "df2" → 2)
        int spawnID = 1;
        if (!string.IsNullOrEmpty(localHero.spawnPointId))
        {
            var match = System.Text.RegularExpressions.Regex.Match(localHero.spawnPointId, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int parsedSpawnId))
            {
                spawnID = parsedSpawnId;
            }
            else
            {
                Debug.LogWarning($"[BattleSceneController] spawnPointId '{localHero.spawnPointId}' no contiene número válido. Usando default (1).");
            }
        }

        // Mapear Side a teamID
        int teamID = _currentBattleData.playerSide(localHeroName) == Side.Defenders ? 2 : 1;

        // Actualizar el System(ECS) a través de EntityManager
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(DataContainerComponent));

        if (!query.IsEmpty)
        {
            var entity = query.GetSingletonEntity();
            var data = em.GetComponentData<DataContainerComponent>(entity);

            data.selectedSpawnID = spawnID;
            data.teamID = teamID;
            // Asegurar isReady=true para iniciar
            data.isReady = true;

            if (localHero.squadInstances != null && localHero.squadInstances.Count > 0)
                data.selectedSquadBaseID = new FixedString64Bytes(localHero.squadInstances[0].baseSquadID);

            em.SetComponentData(entity, data);
            Debug.Log($"[BattleSceneController] ECS DataContainerComponent sync: spawnID={spawnID}, teamID={teamID}");
        }
        else
        {
            Debug.LogError("[BattleSceneController] No se encontró el singleton DataContainerComponent para sincronizar.");
        }
    }

    /// <summary>
    /// Muestra en consola un resumen de los datos de batalla recibidos.
    /// </summary>
    private void LogBattleData()
    {
        Debug.Log($"[BattleSceneController] === Resumen de Batalla ===");
        Debug.Log($"[BattleSceneController] BattleID: {_currentBattleData.battleID}");
        Debug.Log($"[BattleSceneController] Mapa: {_currentBattleData.mapData?.name ?? "N/A"}");
        Debug.Log($"[BattleSceneController] Atacantes: {_currentBattleData.attackers.Count}");
        Debug.Log($"[BattleSceneController] Defensores: {_currentBattleData.defenders.Count}");

        // Log del héroe local
        string localHeroName = PlayerSessionService.SelectedHero?.heroName ?? "N/A";
        BattleHeroData localHero = _currentBattleData.findHeroDataByName(localHeroName);
        if (localHero != null)
        {
            Debug.Log($"[BattleSceneController] Héroe local: {localHero.heroName} (Nivel {localHero.level})");
            Debug.Log($"[BattleSceneController]   Clase: {localHero.classID}");
            Debug.Log($"[BattleSceneController]   Squads: {localHero.squadInstances.Count}");
            Debug.Log($"[BattleSceneController]   SpawnPoint: {localHero.spawnPointId}");
            Debug.Log($"[BattleSceneController]   Bando: {_currentBattleData.playerSide(localHeroName)}");

            foreach (var squad in localHero.squadInstances)
            {
                Debug.Log($"[BattleSceneController]     Squad: {squad.baseSquadID} (Lvl {squad.level}, Units: {squad.unitsInSquad})");
            }
        }
        else
        {
            Debug.LogWarning($"[BattleSceneController] Héroe local '{localHeroName}' no encontrado en BattleData");
        }

        Debug.Log($"[BattleSceneController] === Fin Resumen ===");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Acceso a los datos de batalla actuales.
    /// </summary>
    public BattleData CurrentBattleData => _currentBattleData;

    #endregion
}
