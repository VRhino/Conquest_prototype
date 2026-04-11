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

    private readonly Queue<FCTEntry> _pool = new Queue<FCTEntry>();

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

    public void Spawn(Vector3 worldPos, DamageCategory type, float dmgValue)
    {
        FCTCategoryEntry entry = categoryConfig != null ? categoryConfig.GetEntry(type) : null;
        FCTEntry fct = _pool.Count > 0 ? _pool.Dequeue() : CreateOverflow();
        fct.Activate(worldPos, entry, dmgValue);
    }

    private void ReturnToPool(FCTEntry entry)
    {
        entry.gameObject.SetActive(false);
        _pool.Enqueue(entry);
    }

    private FCTEntry CreateOverflow()
    {
        FCTEntry entry = Instantiate(prefab, transform);
        entry.OnRelease = () => ReturnToPool(entry);
        return entry;
    }
}
