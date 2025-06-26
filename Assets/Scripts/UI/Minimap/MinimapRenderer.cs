using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders minimap icons for entities with <see cref="MinimapIconComponent"/>.
/// Icons are pooled and updated each frame.
/// </summary>
public class MinimapRenderer : MonoBehaviour
{
    [SerializeField] Camera minimapCamera;
    [SerializeField] RectTransform iconContainer;
    [Header("Icon Prefabs")]
    [SerializeField] Image heroIconPrefab;
    [SerializeField] Image squadIconPrefab;
    [SerializeField] Image capturePointIconPrefab;
    [SerializeField] Image supplyPointIconPrefab;

    struct IconEntry
    {
        public Image image;
        public MinimapIconType type;
    }

    readonly Dictionary<Entity, IconEntry> _activeIcons = new();
    readonly Dictionary<MinimapIconType, Stack<Image>> _pools = new();

    void Awake()
    {
        foreach (MinimapIconType type in Enum.GetValues(typeof(MinimapIconType)))
            _pools[type] = new Stack<Image>();
    }

    void Update()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<MinimapIconComponent>(),
            ComponentType.ReadOnly<LocalTransform>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<MinimapIconComponent> comps = query.ToComponentDataArray<MinimapIconComponent>(Allocator.Temp);
        using NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        var seen = new HashSet<Entity>();

        for (int i = 0; i < entities.Length; i++)
        {
            Entity ent = entities[i];
            var data = comps[i];
            data.worldPosition = transforms[i].Position;
            em.SetComponentData(ent, data);
            seen.Add(ent);

            if (!_activeIcons.TryGetValue(ent, out IconEntry entry))
            {
                entry = new IconEntry { image = CreateIcon(data.iconType), type = data.iconType };
                if (entry.image != null)
                    _activeIcons.Add(ent, entry);
            }

            if (entry.image == null)
                continue;

            entry.image.color = GetColor(data.teamAffiliation);
            Vector3 viewPos = minimapCamera.WorldToViewportPoint(data.worldPosition);
            Vector2 anchored = new Vector2(
                (viewPos.x - 0.5f) * iconContainer.rect.width,
                (viewPos.y - 0.5f) * iconContainer.rect.height);
            entry.image.rectTransform.anchoredPosition = anchored;
        }

        // Remove icons for entities no longer present
        var toRemove = new List<Entity>();
        foreach (var kv in _activeIcons)
        {
            if (!seen.Contains(kv.Key))
            {
                ReturnIcon(kv.Value);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var e in toRemove)
            _activeIcons.Remove(e);
    }

    Image CreateIcon(MinimapIconType type)
    {
        if (_pools[type].Count > 0)
            return Activate(_pools[type].Pop());

        Image prefab = type switch
        {
            MinimapIconType.Hero => heroIconPrefab,
            MinimapIconType.Squad => squadIconPrefab,
            MinimapIconType.CapturePoint => capturePointIconPrefab,
            MinimapIconType.SupplyPoint => supplyPointIconPrefab,
            _ => heroIconPrefab
        };

        if (prefab == null || iconContainer == null)
            return null;

        return Instantiate(prefab, iconContainer);
    }

    void ReturnIcon(IconEntry entry)
    {
        if (entry.image == null)
            return;
        entry.image.gameObject.SetActive(false);
        _pools[entry.type].Push(entry.image);
    }

    static Image Activate(Image img)
    {
        if (img != null)
            img.gameObject.SetActive(true);
        return img;
    }

    static Color GetColor(Team team)
    {
        return team switch
        {
            Team.TeamA => Color.blue,
            Team.TeamB => Color.red,
            _ => Color.gray
        };
    }
}
