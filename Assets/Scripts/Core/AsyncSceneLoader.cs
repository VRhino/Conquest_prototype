using System;
using System.Collections;
using System.Reflection;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Utility to load Unity scenes asynchronously and update the current
/// <see cref="GameStateComponent"/> with the target <see cref="GamePhase"/>.
/// </summary>
public class AsyncSceneLoader : MonoBehaviour
{
    static AsyncSceneLoader _instance;

    [SerializeField]
    GameObject _loadingScreen;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Loads the given scene and sets the target game phase once loading is
    /// complete.
    /// </summary>
    /// <param name="sceneName">Name of the scene to load.</param>
    /// <param name="targetPhase">Phase that will be active after the scene loads.</param>
    public static void LoadScene(string sceneName, GamePhase targetPhase)
    {
        if (_instance == null)
        {
            var go = new GameObject(nameof(AsyncSceneLoader));
            _instance = go.AddComponent<AsyncSceneLoader>();
            DontDestroyOnLoad(go);
        }

        _instance.StartCoroutine(_instance.LoadSceneRoutine(sceneName, targetPhase));
    }

    IEnumerator LoadSceneRoutine(string sceneName, GamePhase targetPhase)
    {
        if (_loadingScreen != null)
            _loadingScreen.SetActive(true);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
            yield return null;

        if (_loadingScreen != null)
            _loadingScreen.SetActive(false);

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        Entity stateEntity;
        var q = em.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (q.IsEmptyIgnoreFilter)
        {
            stateEntity = em.CreateEntity(typeof(GameStateComponent));
        }
        else
        {
            stateEntity = q.GetSingletonEntity();
        }
        em.SetComponentData(stateEntity, new GameStateComponent { currentPhase = targetPhase });

        // Optional integration with SceneFlowManager using reflection
        Type managerType = Type.GetType("SceneFlowManager");
        if (managerType != null)
        {
            UnityEngine.Object manager = FindObjectOfType(managerType);
            if (manager != null)
            {
                MethodInfo method = managerType.GetMethod("OnPhaseChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(manager, new object[] { targetPhase });
                }
            }
        }
    }
}
