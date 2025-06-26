using UnityEngine;

/// <summary>
/// Base component for objects managed by <see cref="ObjectPoolSystem"/>.
/// Handles automatic return to the pool if a lifetime is specified.
/// </summary>
public class PoolableObject : MonoBehaviour
{
    [HideInInspector]
    public string poolKey = string.Empty;

    [Tooltip("Seconds before automatically returning to the pool. 0 = manual return")]
    public float autoReturnTime = 0f;

    float _timer;

    /// <summary>Called when the object is retrieved from the pool.</summary>
    public virtual void OnSpawned()
    {
        if (autoReturnTime > 0f)
            _timer = autoReturnTime;
    }

    void Update()
    {
        if (autoReturnTime <= 0f)
            return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
            ReturnToPool();
    }

    /// <summary>Returns this instance back to its pool.</summary>
    public void ReturnToPool()
    {
        if (ObjectPoolSystem.Instance != null)
            ObjectPoolSystem.Instance.ReturnToPool(poolKey, gameObject);
        else
            Destroy(gameObject);
    }
}
