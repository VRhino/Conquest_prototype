using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that stores and reuses temporary <see cref="GameObject"/> instances.
/// Used for projectiles and ability effects to avoid frequent allocations.
/// </summary>
public class ObjectPoolSystem : MonoBehaviour
{
    /// <summary>Global access instance.</summary>
    public static ObjectPoolSystem Instance { get; private set; }

    readonly Dictionary<string, Queue<GameObject>> _pools = new();
    readonly Dictionary<string, GameObject> _prefabs = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Preloads a number of instances for a given prefab.
    /// </summary>
    /// <param name="key">Pool identifier.</param>
    /// <param name="prefab">Prefab to instantiate.</param>
    /// <param name="count">Number of instances to create.</param>
    public void WarmUpPool(string key, GameObject prefab, int count)
    {
        if (string.IsNullOrEmpty(key) || prefab == null || count <= 0)
            return;

        if (!_pools.TryGetValue(key, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>(count);
            _pools[key] = queue;
        }

        if (!_prefabs.ContainsKey(key))
            _prefabs[key] = prefab;

        while (queue.Count < count)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            RegisterPoolKey(obj, key);
            queue.Enqueue(obj);
        }
    }

    /// <summary>
    /// Retrieves an instance from the pool or creates one if empty.
    /// </summary>
    /// <param name="key">Pool identifier.</param>
    public GameObject GetFromPool(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (!_pools.TryGetValue(key, out Queue<GameObject> queue))
            queue = _pools[key] = new Queue<GameObject>();

        GameObject obj;
        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
            if (obj == null)
                return GetFromPool(key);
        }
        else if (_prefabs.TryGetValue(key, out GameObject prefab))
        {
            obj = Instantiate(prefab);
            RegisterPoolKey(obj, key);
        }
        else
        {
            return null;
        }

        obj.SetActive(true);
        PoolableObject poolable = obj.GetComponent<PoolableObject>();
        if (poolable != null)
            poolable.OnSpawned();
        return obj;
    }

    /// <summary>
    /// Returns the object to its pool and deactivates it.
    /// </summary>
    /// <param name="key">Pool identifier.</param>
    /// <param name="obj">Instance to recycle.</param>
    public void ReturnToPool(string key, GameObject obj)
    {
        if (obj == null || string.IsNullOrEmpty(key))
            return;

        if (!_pools.TryGetValue(key, out Queue<GameObject> queue))
            queue = _pools[key] = new Queue<GameObject>();

        obj.SetActive(false);
        queue.Enqueue(obj);
    }

    static void RegisterPoolKey(GameObject obj, string key)
    {
        PoolableObject poolable = obj.GetComponent<PoolableObject>();
        if (poolable != null)
            poolable.poolKey = key;
    }
}
