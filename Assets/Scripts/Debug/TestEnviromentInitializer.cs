// This class is responsible for initializing the test environment
using System.Collections.Generic;
using UnityEngine;

public enum EnemySquadMode { Random, Fixed }

public class TestEnvironmentInitializer : MonoBehaviour
{
    [SerializeField] private int attackers = 3;
    [SerializeField] private int defenders = 3;
    [SerializeField] private Team playerTeam = Team.TeamA;
    [SerializeField] private EnemySquadMode enemySquadMode = EnemySquadMode.Random;
    [SerializeField] private SquadData enemySquadOverride;
    [Tooltip("Solo al abrir esta escena directamente (sin datos de batalla del juego ni del motor): si hay alguna, el jugador " +
             "(su héroe del player_save) y cada bot llevan una escuadra completa de cada una, a nivel 1. Vacía: como siempre.")]
    [SerializeField] private List<SquadData> tropasDePrueba = new List<SquadData>();

    public void SetupTestEnvironment()
    {
        // Desde el mundo de BronzeAge (el interior de un campamento) el héroe es el del motor: nada de player_save.json.
        if (ConquestTactics.Mundo.SelectorDePersonaje.Interior != null) return;
        // Solo inicializar con datos de test si no hay datos de transición
        if (!BattleTransitionData.Instance.HasBattleData() && !PlayerSessionService.IsSessionActive)
        {
            LoadSystem.LoadDataForTesting(out HeroData localHero, out PlayerData player);
            PlayerSessionService.SetPlayer(player);
            PlayerSessionService.SetSelectedHero(localHero);
        }
    }

    public void SetGamePhase(GamePhase phase)
    {
        SceneTransitionService.UpdateGamePhase(phase);
    }

    public BattleData GenerateBattleData(HeroData localHero)
    {
        var battleData = BattleDebugCreator.CreateBattleWithLocalHero(localHero, playerTeam, attackers, defenders, enemySquadMode, enemySquadOverride);
        if (battleData != null && tropasDePrueba != null && tropasDePrueba.Count > 0)
            BattleDebugCreator.PonerTropasDePrueba(battleData, tropasDePrueba);
        return battleData;
    }

}
