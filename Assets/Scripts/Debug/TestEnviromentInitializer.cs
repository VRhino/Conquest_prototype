// This class is responsible for initializing the test environment
using UnityEngine;

public class TestEnvironmentInitializer : MonoBehaviour
{
    public void SetupTestEnvironment()
    {
        // Solo inicializar con datos de test si no hay datos de transición
        if (!BattleTransitionData.Instance.HasBattleData())
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
        return BattleDebugCreator.CreateBattleWithLocalHero(localHero);
    }

}
