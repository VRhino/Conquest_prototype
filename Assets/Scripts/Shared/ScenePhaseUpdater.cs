using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePhaseUpdater : MonoBehaviour
{
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive content (terrain / ECS subscenes) must not replace the owning scene phase.
        if (mode == LoadSceneMode.Additive) return;
        // Actualizar GamePhase basado en la escena cargada, por si no se hizo antes
        SceneTransitionService.UpdateGamePhase(scene.name);
    }
}