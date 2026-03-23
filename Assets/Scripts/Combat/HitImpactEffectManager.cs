using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour that manages a pool of hit-impact VFX.
/// Place in BattleScene (NOT in a DOTS subscene), alongside FloatingCombatTextManager.
/// Assign the Hit_02 (or any ParticleSystem) prefab in the Inspector.
/// </summary>
public class HitImpactEffectManager : MonoBehaviour
{
    public static HitImpactEffectManager Instance { get; private set; }

    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private int poolSize = 15;

    private readonly Queue<ParticleSystem> _pool = new Queue<ParticleSystem>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (hitEffectPrefab == null)
        {
            Debug.LogError("[HitImpactEffectManager] hitEffectPrefab NOT assigned — hit VFX will not work.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            _pool.Enqueue(CreateInstance());
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Spawn a hit impact VFX at the given world position.
    /// The effect faces the main camera automatically.
    /// </summary>
    public void Spawn(Vector3 worldPos)
    {
        if (hitEffectPrefab == null) return;

        ParticleSystem ps = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();
        ps.transform.position = worldPos;

        // Face camera so the flash/glow is always visible
        if (Camera.main != null)
            ps.transform.LookAt(Camera.main.transform);

        ps.gameObject.SetActive(true);
        ps.Play(true);
    }

    private ParticleSystem CreateInstance()
    {
        GameObject go = Instantiate(hitEffectPrefab, transform);
        go.SetActive(false);

        // Make sure main module stops and we can detect when it's done
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = go.GetComponentInChildren<ParticleSystem>();
        }

        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;

        // Attach recycler
        var recycler = go.AddComponent<HitEffectRecycler>();
        recycler.Init(this, ps);

        return ps;
    }

    internal void ReturnToPool(ParticleSystem ps)
    {
        ps.gameObject.SetActive(false);
        _pool.Enqueue(ps);
    }
}

/// <summary>
/// Auto-returns the ParticleSystem to the pool when it finishes playing.
/// </summary>
public class HitEffectRecycler : MonoBehaviour
{
    private HitImpactEffectManager _manager;
    private ParticleSystem _ps;

    public void Init(HitImpactEffectManager manager, ParticleSystem ps)
    {
        _manager = manager;
        _ps = ps;
    }

    private void OnParticleSystemStopped()
    {
        _manager.ReturnToPool(_ps);
    }
}
