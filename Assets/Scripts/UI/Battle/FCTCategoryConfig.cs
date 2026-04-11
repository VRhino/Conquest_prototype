using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FCTCategoryEntry
{
    public DamageCategory category;
    public Sprite icon;
    [Tooltip("Texto prefijo. Vacío = solo número.")]
    public string label;
    public Color color = Color.white;
    [Tooltip("Multiplicador sobre baseFontSize (1 = tamaño normal).")]
    public float fontScale = 1f;
    [Tooltip("Mostrar el valor numérico junto al label.")]
    public bool showValue = true;
}

[CreateAssetMenu(fileName = "FCTCategoryConfig", menuName = "Conquest/FCT Category Config")]
public class FCTCategoryConfig : ScriptableObject
{
    public List<FCTCategoryEntry> entries = new List<FCTCategoryEntry>();

    public FCTCategoryEntry GetEntry(DamageCategory category)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].category == category) return entries[i];
        }
        return null; // caller uses fallback
    }
}
