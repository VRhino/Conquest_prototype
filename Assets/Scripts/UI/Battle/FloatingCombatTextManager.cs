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

    [Tooltip("Sprites indexados por DamageCategory. El tamaño del array se ajusta automáticamente al enum.")]
    [SerializeField] private Sprite[] icons = new Sprite[System.Enum.GetValues(typeof(DamageCategory)).Length];

    [SerializeField] private int poolSize = 20;

    private readonly Queue<FCTEntry> _pool = new Queue<FCTEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (prefab == null)
        {
            Debug.LogError("[BattleTestDebug] FCTManager.Awake: prefab NO asignado — el FCT no funcionará.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            FCTEntry entry = Instantiate(prefab, transform);
            entry.gameObject.SetActive(false);
            entry.OnRelease = () => ReturnToPool(entry);
            _pool.Enqueue(entry);
        }

        Debug.Log($"[BattleTestDebug] FCTManager.Awake: singleton OK, pool={_pool.Count} entradas.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Spawn(Vector3 worldPos, DamageCategory type, float dmgValue)
    {
        Debug.Log($"[BattleTestDebug] FCTManager.Spawn: type={type}, dmg={dmgValue:F1}, pos={worldPos}, poolLeft={_pool.Count}");
        Sprite iconSprite = (int)type < icons.Length ? icons[(int)type] : null;
        FCTEntry entry = _pool.Count > 0 ? _pool.Dequeue() : CreateOverflow();
        entry.Activate(worldPos, type, dmgValue, iconSprite);
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
