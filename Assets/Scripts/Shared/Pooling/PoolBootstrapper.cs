using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Initializes object pools at scene load time.
/// </summary>
public class PoolBootstrapper : MonoBehaviour
{
    [System.Serializable]
    public class PoolEntry
    {
        public string key = string.Empty;
        public GameObject prefab = null;
        public int preloadCount = 0;
    }

    [SerializeField] List<PoolEntry> pools = new();

    void Awake()
    {
        if (ObjectPoolSystem.Instance == null)
        {
            var manager = new GameObject(nameof(ObjectPoolSystem));
            manager.AddComponent<ObjectPoolSystem>();
        }

        foreach (var entry in pools)
        {
            if (entry.prefab != null && !string.IsNullOrEmpty(entry.key))
                ObjectPoolSystem.Instance.WarmUpPool(entry.key, entry.prefab, entry.preloadCount);
        }
    }
}
