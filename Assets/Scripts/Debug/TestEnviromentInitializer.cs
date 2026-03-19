// This class is responsible for initializing the test environment
using UnityEngine;

public class TestEnvironmentInitializer : MonoBehaviour
{
    [SerializeField] private int attackers = 3;
    [SerializeField] private int defenders = 3;
    [SerializeField] private Team playerTeam = Team.TeamA;

    public void SetupTestEnvironment()
    {
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
        return BattleDebugCreator.CreateBattleWithLocalHero(localHero, playerTeam, attackers, defenders);
    }

}
