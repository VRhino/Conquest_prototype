using UnityEngine;

public class HeroPrefabAuthoring : MonoBehaviour
{
    public GameObject heroPrefab;

    void Awake()
    {
        Debug.Log($"[HeroPrefabAuthoring] Awake en GameObject '{gameObject.name}' con heroPrefab asignado: {(heroPrefab != null ? heroPrefab.name : "null")}");
    }
}
