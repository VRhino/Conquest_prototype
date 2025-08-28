using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de datos de todos los tipos de tiendas (StoreData) disponibles en el juego.
/// </summary>
/// 
[CreateAssetMenu(menuName = "NPC/Store Database")]
public class StoreDatabase : ScriptableObject
{
    public List<StoreData> allStores;

    public StoreData GetStoreById(string id)
    {
        return allStores.Find(store => store.storeId == id);
    }
    private static StoreDatabase _instance;
    public static StoreDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<StoreDatabase>("Data/Store/StoreDatabase");
                if (_instance == null)
                {
                    Debug.LogError("[StoreDatabase] No StoreDatabase found in Resources/Data/ folder!");
                }
            }
            return _instance;
        }
    }
}
