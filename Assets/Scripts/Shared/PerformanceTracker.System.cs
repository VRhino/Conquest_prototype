#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Text;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif
using TMPro;

/// <summary>
/// Displays real time performance metrics in a small on-screen panel.
/// Active only in development builds or the Unity editor.
/// </summary>
public class PerformanceTrackerSystem : MonoBehaviour
{
    [SerializeField] float updateInterval = 1f;

    Canvas _canvas;
    TMP_Text _label;
    RectTransform _panel;
    StringBuilder _builder;
    float _timer;
    bool _visible = true;
    EntityQuery _entityQuery;

    void Awake()
    {
        _builder = new StringBuilder(256);
        _canvas = GetComponentInChildren<Canvas>();
        if (_canvas == null)
        {
            GameObject canvasGO = new GameObject("PerfCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(canvasGO);
        }

        _panel = new GameObject("PerfPanel").AddComponent<RectTransform>();
        _panel.SetParent(_canvas.transform, false);
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchoredPosition = new Vector2(10f, -10f);

        _label = new GameObject("PerfLabel").AddComponent<TMP_Text>();
        _label.fontSize = 14;
        _label.alignment = TextAlignmentOptions.TopLeft;
        _label.rectTransform.SetParent(_panel, false);

        _entityQuery = World.DefaultGameObjectInjectionWorld.EntityManager.UniversalQuery;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
            Toggle();

        _timer += Time.unscaledDeltaTime;
        if (_timer >= updateInterval)
        {
            _timer = 0f;
            UpdateStats();
        }
    }

    void Toggle()
    {
        _visible = !_visible;
        if (_panel != null)
            _panel.gameObject.SetActive(_visible);
    }

    void UpdateStats()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        int entityCount = _entityQuery.CalculateEntityCount();

        float fps = 1f / Time.unscaledDeltaTime;
        float frameMs = Time.unscaledDeltaTime * 1000f;
        long mem = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        int goCount = FindObjectsOfType<GameObject>(false).Length;

        _builder.Clear();
        _builder.AppendLine($"FPS: {fps:F1}");
        _builder.AppendLine($"Frame: {frameMs:F2} ms");
        _builder.AppendLine($"Entities: {entityCount}");
        _builder.AppendLine($"GC: {mem} MB");
        _builder.AppendLine($"GameObjects: {goCount}");
#if UNITY_EDITOR
        _builder.AppendLine($"Draw Calls: {UnityStats.drawCalls}");
        _builder.AppendLine($"Batches: {UnityStats.batches}");
#endif
        if (_label != null)
            _label.text = _builder.ToString();
    }
}
#endif
