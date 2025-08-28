using System.Collections.Generic;
using UnityEngine;
using Data.Items;

/// <summary>
/// ScriptableObject que define los datos de una tienda: título y lista de productos.
/// Permite crear N tiendas distintas desde el editor.
/// </summary>
[CreateAssetMenu(menuName = "NPC/StoreData", fileName = "StoreData")]
public class StoreData : ScriptableObject
{
    [Header("ID")]
    public string storeId;

    [Header("Store Info")]
    public string storeTitle;

    [Header("Products")]
    public List<string> productIds = new();
}
