using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour que gestiona el pool de Floating Combat Text.
/// Debe colocarse en BattleScene (NO en DOTSWorld subscene).
/// </summary>
public class FloatingCombatTextManager : MonoBehaviour
{
    public static FloatingCombatTextManager Instance { get; private set; }

    [SerializeField] private FCTEntry prefab;

    [SerializeField] private FCTCategoryConfig categoryConfig;

    [SerializeField] private int poolSize = 20;

    [Tooltip("Textos a la vez como máximo; los que llegan con el tope lleno no se pintan (C12).")]
    [SerializeField, Min(1)] private int maxActive = 30;

    private readonly Queue<FCTEntry> _pool = new Queue<FCTEntry>();
    private int _active;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (prefab == null)
        {

            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            FCTEntry entry = Instantiate(prefab, transform);
            entry.gameObject.SetActive(false);
            entry.OnRelease = () => ReturnToPool(entry);
            _pool.Enqueue(entry);
        }


    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Shows a hit's text if <see cref="FloatingTextRelevance.Shows"/> lets it: the local player is in the hit, or
    /// its category is shown for others, and the cap is not full.</summary>
    public void Spawn(Vector3 worldPos, DamageCategory type, float dmgValue, bool involvesLocal)
    {
        if (prefab == null) return;
        FCTCategoryEntry entry = categoryConfig != null ? categoryConfig.GetEntry(type) : null;
        if (!FloatingTextRelevance.Shows(involvesLocal, entry != null && entry.showForOthers, _active, maxActive)) return;
        FCTEntry fct = _pool.Count > 0 ? _pool.Dequeue() : CreateOverflow();
        _active++;
        fct.Activate(worldPos, entry, dmgValue);
    }

    /// <summary>Texts on screen now (C12).</summary>
    public int ActiveCount => _active;

    private void ReturnToPool(FCTEntry entry)
    {
        entry.gameObject.SetActive(false);
        _active = Mathf.Max(0, _active - 1);
        _pool.Enqueue(entry);
    }

    private FCTEntry CreateOverflow()
    {
        FCTEntry entry = Instantiate(prefab, transform);
        entry.OnRelease = () => ReturnToPool(entry);
        return entry;
    }
}
