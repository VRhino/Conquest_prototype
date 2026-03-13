using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Controlador principal de la escena de batalla.
/// Recibe BattleData desde BattleTransitionData y coordina la inicialización de la batalla.
/// </summary>
public class BattleSceneController : MonoBehaviour
{
    #region Private Fields

    private BattleData _currentBattleData;

    [SerializeField] private TextMeshProUGUI _battleTimerDisplay;
    [SerializeField] private GameObject _loadingScreen;

    private TimerController _timerController;
    private bool _loadingScreenDismissed;

    private const int MaxBattleDurationSeconds = 1800; // 30 min cap

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

    void Update()
    {
        if (_loadingScreenDismissed || _loadingScreen == null) return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(typeof(IsLocalPlayer), typeof(HeroVisualInstance));
        if (!query.IsEmpty)
        {
            _loadingScreen.SetActive(false);
            _loadingScreenDismissed = true;
            Debug.Log("[BattleSceneController] Hero visual ready — loading screen dismissed.");
        }
        query.Dispose();
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

        // Spawnear héroes remotos (oponentes y aliados)
        SpawnRemoteHeroes();

        // Inicializar timer de batalla
        InitializeBattleTimer();
    }

    /// <summary>
    /// Configura e inicia el timer de batalla.
    /// </summary>
    private void InitializeBattleTimer()
    {
        int battleSeconds = _currentBattleData.BattleTimer;
        if (battleSeconds <= 0 && _currentBattleData.mapData != null)
            battleSeconds = _currentBattleData.mapData.battleDuration;
        if (battleSeconds <= 0)
            battleSeconds = 900; // fallback 15 min

        _timerController = gameObject.AddComponent<TimerController>();
        _timerController.Initialize(battleSeconds, _battleTimerDisplay);
        _timerController.OnTimerFinished += OnBattleTimerExpired;
        _timerController.SetCountDownSecs(battleSeconds);

        Debug.Log($"[BattleSceneController] Battle timer iniciado: {battleSeconds}s");
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
            {
                data.selectedSquadBaseID = new FixedString64Bytes(localHero.squadInstances[0].baseSquadID);

                // Asignar IDs enteros secuenciales (0, 1, 2, ...) a cada instancia de escuadra.
                // ID 0 = escuadra activa → coincide con HeroSpawnSystem que hardcodea instanceId = 0.
                data.selectedSquads.Clear();
                for (int i = 0; i < localHero.squadInstances.Count; i++)
                    data.selectedSquads.Add(i);

                // Poblar el buffer de mapping usando las instancias directamente
                if (em.HasBuffer<SquadIdMapElement>(entity))
                {
                    var mapBuffer = em.GetBuffer<SquadIdMapElement>(entity);
                    mapBuffer.Clear();
                    for (int i = 0; i < localHero.squadInstances.Count; i++)
                    {
                        mapBuffer.Add(new SquadIdMapElement
                        {
                            squadId = i,
                            baseSquadID = new FixedString64Bytes(localHero.squadInstances[i].baseSquadID)
                        });
                    }
                }
            }

            em.SetComponentData(entity, data);
            Debug.Log($"[BattleSceneController] ECS DataContainerComponent sync: spawnID={spawnID}, teamID={teamID}");
        }
        else
        {
            Debug.LogError("[BattleSceneController] No se encontró el singleton DataContainerComponent para sincronizar.");
        }
    }

    /// <summary>
    /// Inicia la coroutine que spawnea los héroes remotos (oponentes y aliados no locales).
    /// Se difiere ~1.5s para que el mundo ECS y la subscena estén completamente listos.
    /// </summary>
    private void SpawnRemoteHeroes()
    {
        StartCoroutine(SpawnRemoteHeroesCoroutine());
    }

    private IEnumerator SpawnRemoteHeroesCoroutine()
    {
        // Esperar a que el mundo ECS esté listo (igual que HeroSpawnSystem que espera 1s)
        yield return new WaitForSeconds(1.5f);

        var localHeroName = PlayerSessionService.SelectedHero?.heroName;
        if (string.IsNullOrEmpty(localHeroName)) yield break;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) yield break;

        var em = world.EntityManager;

        // Obtener el prefab del héroe
        var prefabQuery = em.CreateEntityQuery(typeof(HeroPrefabComponent));
        if (prefabQuery.IsEmpty)
        {
            Debug.LogWarning("[BattleSceneController] HeroPrefabComponent no encontrado, no se pueden spawnear héroes remotos.");
            prefabQuery.Dispose();
            yield break;
        }
        var heroPrefabComp = prefabQuery.GetSingleton<HeroPrefabComponent>();
        prefabQuery.Dispose();

        // Obtener spawn points
        var spawnPointQuery = em.CreateEntityQuery(typeof(SpawnPointComponent));
        var spawnPoints = spawnPointQuery.ToComponentDataArray<SpawnPointComponent>(Allocator.Temp);
        spawnPointQuery.Dispose();

        // Slot counter: clave = spawnID * 100 + teamID
        // Reservar slot 0 para el héroe local en su spawn point
        var slotCounter = new Dictionary<int, int>();
        BattleHeroData localHero = _currentBattleData.findHeroDataByName(localHeroName);
        if (localHero != null)
        {
            int localSpawnID = 1;
            if (!string.IsNullOrEmpty(localHero.spawnPointId))
            {
                var m = System.Text.RegularExpressions.Regex.Match(localHero.spawnPointId, @"\d+");
                if (m.Success && int.TryParse(m.Value, out int parsed))
                    localSpawnID = parsed;
            }
            int localTeamID = _currentBattleData.playerSide(localHeroName) == Side.Defenders ? 2 : 1;
            slotCounter[localSpawnID * 100 + localTeamID] = 1; // slot 0 ya ocupado por el local
        }

        // Colectar héroes remotos: todos excepto el jugador local
        var remoteHeroes = new List<(BattleHeroData heroData, int teamID)>();
        foreach (var hero in _currentBattleData.attackers)
        {
            if (hero.heroName != localHeroName)
                remoteHeroes.Add((hero, 1)); // teamID 1 = TeamA = Attackers
        }
        foreach (var hero in _currentBattleData.defenders)
        {
            if (hero.heroName != localHeroName)
                remoteHeroes.Add((hero, 2)); // teamID 2 = TeamB = Defenders
        }

        foreach (var (heroData, teamID) in remoteHeroes)
        {
            // Parsear spawnID del héroe remoto para asignar su slot lateral
            int spawnID = 1;
            if (!string.IsNullOrEmpty(heroData.spawnPointId))
            {
                var m = System.Text.RegularExpressions.Regex.Match(heroData.spawnPointId, @"\d+");
                if (m.Success && int.TryParse(m.Value, out int parsed))
                    spawnID = parsed;
            }
            int key = spawnID * 100 + teamID;
            if (!slotCounter.TryGetValue(key, out int slotIndex))
                slotIndex = 0;
            slotCounter[key] = slotIndex + 1;

            SpawnRemoteHero(em, heroPrefabComp, spawnPoints, heroData, teamID, slotIndex);
        }

        spawnPoints.Dispose();
        Debug.Log($"[BattleSceneController] Héroes remotos spawneados: {remoteHeroes.Count}");
    }

    private void SpawnRemoteHero(EntityManager em, HeroPrefabComponent heroPrefabComp,
        NativeArray<SpawnPointComponent> spawnPoints, BattleHeroData heroData, int teamID, int slotIndex = 0)
    {
        // Parsear spawnPointId a int (ej: "at1" → 1, "df2" → 2)
        int spawnID = 1;
        if (!string.IsNullOrEmpty(heroData.spawnPointId))
        {
            var match = System.Text.RegularExpressions.Regex.Match(heroData.spawnPointId, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int parsed))
                spawnID = parsed;
        }

        // Buscar spawn point correspondiente
        SpawnPointComponent selectedSpawn = default;
        bool found = false;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var sp = spawnPoints[i];
            if (sp.spawnID == spawnID && sp.teamID == teamID && sp.isActive)
            {
                selectedSpawn = sp;
                found = true;
                break;
            }
        }
        if (!found)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var sp = spawnPoints[i];
                if (sp.teamID == teamID && sp.isActive)
                {
                    selectedSpawn = sp;
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[BattleSceneController] No se encontró spawn point para héroe remoto '{heroData.heroName}' (team={teamID}, spawnID={spawnID})");
            return;
        }

        // Calcular posición y rotación
        var spawnPosition = selectedSpawn.position;
        spawnPosition.y = FormationPositionCalculator.calculateTerraindHeight(spawnPosition);
        quaternion spawnRotation = CalculateRemoteSpawnRotation(em, spawnPosition, teamID);

        // Aplicar offset lateral según el slot para evitar superposición de héroes en el mismo spawn point
        if (slotIndex != 0)
        {
            float spacing = 10f; // fallback si el singleton no está listo
            var configQuery = em.CreateEntityQuery(typeof(SquadSpawnConfigComponent));
            if (configQuery.CalculateEntityCount() > 0)
                spacing = configQuery.GetSingleton<SquadSpawnConfigComponent>().heroSlotSpacing;
            configQuery.Dispose();

            float3 right = math.cross(math.up(), math.forward(spawnRotation));
            spawnPosition += right * SlotOffset(slotIndex, spacing);
            spawnPosition.y = FormationPositionCalculator.calculateTerraindHeight(spawnPosition);
        }

        // Instanciar entidad héroe desde el prefab
        var heroEntity = em.Instantiate(heroPrefabComp.prefab);

        // Posicionar
        em.SetComponentData(heroEntity, new LocalTransform
        {
            Position = spawnPosition,
            Rotation = spawnRotation,
            Scale = 1f
        });

        // Remover IsLocalPlayer para que no reciba input ni sea procesado como jugador local
        if (em.HasComponent<IsLocalPlayer>(heroEntity))
            em.RemoveComponent<IsLocalPlayer>(heroEntity);

        // Remover HeroInputComponent para aislar completamente del input
        if (em.HasComponent<HeroInputComponent>(heroEntity))
            em.RemoveComponent<HeroInputComponent>(heroEntity);

        // Asignar team correcto
        var team = teamID == 1 ? Team.TeamA : Team.TeamB;
        em.SetComponentData(heroEntity, new TeamComponent { value = team });

        // Marcar como spawneado
        if (em.HasComponent<HeroSpawnComponent>(heroEntity))
        {
            var spawnComp = em.GetComponentData<HeroSpawnComponent>(heroEntity);
            spawnComp.hasSpawned = true;
            spawnComp.spawnPosition = spawnPosition;
            spawnComp.spawnRotation = spawnRotation;
            spawnComp.spawnId = spawnID;
            em.SetComponentData(heroEntity, spawnComp);
        }

        // Asignar squad si hay instancias disponibles
        if (heroData.squadInstances != null && heroData.squadInstances.Count > 0)
        {
            var targetSquadID = new FixedString64Bytes(heroData.squadInstances[0].baseSquadID);
            var squadQuery = em.CreateEntityQuery(typeof(SquadDataIDComponent));
            var squadEntities = squadQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < squadEntities.Length; i++)
            {
                var idComp = em.GetComponentData<SquadDataIDComponent>(squadEntities[i]);
                if (idComp.id == targetSquadID)
                {
                    em.AddComponentData(heroEntity, new HeroSquadSelectionComponent
                    {
                        squadDataEntity = squadEntities[i],
                        instanceId = 0
                    });
                    break;
                }
            }

            squadEntities.Dispose();
            squadQuery.Dispose();
        }

        Debug.Log($"[BattleSceneController] Héroe remoto '{heroData.heroName}' spawneado en {spawnPosition} (team={teamID})");
    }

    /// <summary>
    /// Devuelve el offset lateral (en metros) para el slot dado usando un patrón alternado centrado en 0.
    /// slot 0 → 0, slot 1 → +spacing, slot 2 → −spacing, slot 3 → +2×spacing, ...
    /// </summary>
    private static float SlotOffset(int slotIndex, float spacing)
    {
        if (slotIndex == 0) return 0f;
        int side = (slotIndex % 2 == 1) ? 1 : -1;
        int row  = (slotIndex + 1) / 2;
        return side * row * spacing;
    }

    /// <summary>
    /// Calcula la rotación de spawn mirando hacia la zona final del equipo enemigo.
    /// Replica la lógica de HeroSpawnSystem.CalculateSpawnRotation.
    /// </summary>
    private quaternion CalculateRemoteSpawnRotation(EntityManager em, float3 spawnPos, int heroTeamId)
    {
        var zoneQuery = em.CreateEntityQuery(typeof(ZoneTriggerComponent), typeof(LocalTransform));
        var zoneEntities = zoneQuery.ToEntityArray(Allocator.Temp);
        quaternion result = quaternion.identity;

        for (int i = 0; i < zoneEntities.Length; i++)
        {
            var zone = em.GetComponentData<ZoneTriggerComponent>(zoneEntities[i]);
            if (zone.isFinal && zone.teamOwner != heroTeamId)
            {
                var zoneTransform = em.GetComponentData<LocalTransform>(zoneEntities[i]);
                float3 direction = zoneTransform.Position - spawnPos;
                direction.y = 0f;
                if (math.lengthsq(direction) > 0.001f)
                    result = quaternion.LookRotationSafe(math.normalize(direction), math.up());
                break;
            }
        }

        zoneEntities.Dispose();
        zoneQuery.Dispose();
        return result;
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

    /// <summary>
    /// Extiende el timer de batalla por la cantidad de segundos indicada, sin superar el máximo de 30 minutos.
    /// </summary>
    public void ExtendTimer(int seconds)
    {
        if (_timerController == null || seconds <= 0) return;

        // Calcular cuánto tiempo queda y cuánto podemos agregar sin superar el cap
        int currentRemaining = _timerController.SecondsRemaining;
        int maxExtension = MaxBattleDurationSeconds - currentRemaining;
        int actualExtension = Mathf.Min(seconds, maxExtension);

        if (actualExtension <= 0)
        {
            Debug.Log("[BattleSceneController] Timer ya está en el máximo (30 min), no se extiende.");
            return;
        }

        _timerController.AddSeconds(actualExtension);
        Debug.Log($"[BattleSceneController] Timer extendido +{actualExtension}s (restante: {currentRemaining + actualExtension}s)");
    }

    #endregion

    #region Timer Events

    /// <summary>
    /// Se invoca cuando el timer de batalla llega a cero. Victoria para los defensores.
    /// </summary>
    private void OnBattleTimerExpired()
    {
        Debug.Log("[BattleSceneController] Timer de batalla expirado — victoria de defensores.");

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(typeof(MatchStateComponent));
        if (!query.IsEmpty)
        {
            var entity = query.GetSingletonEntity();
            var match = em.GetComponentData<MatchStateComponent>(entity);
            match.victoryConditionMet = true;
            match.winnerTeam = 2; // Defenders win on timer expiry
            em.SetComponentData(entity, match);
        }
    }

    #endregion
}
