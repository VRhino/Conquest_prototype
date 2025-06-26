using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Displays available spawn points on the minimap and forwards the
/// player's selection into the DataContainer.
/// </summary>
public class MapUIController : MonoBehaviour
{
    [SerializeField] RectTransform _spawnContainer;
    [SerializeField] SpawnIcon _spawnIconPrefab;

    readonly List<SpawnIcon> _icons = new();

    void Start()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (!SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var dataEntity))
            return;

        var data = em.GetComponentData<DataContainerComponent>(dataEntity);
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<SpawnPointComponent>());
        using NativeArray<SpawnPointComponent> points = query.ToComponentDataArray<SpawnPointComponent>(Allocator.Temp);

        foreach (var sp in points)
        {
            if (sp.teamID != data.teamID || !sp.isActive)
                continue;

            if (_spawnIconPrefab != null && _spawnContainer != null)
            {
                var icon = Instantiate(_spawnIconPrefab, _spawnContainer);
                icon.Initialize(sp.spawnID, this);
                _icons.Add(icon);
            }
        }
    }

    /// <summary>Selects the given spawn point.</summary>
    /// <param name="spawnId">Identifier of the spawn point.</param>
    public void SelectSpawn(int spawnId)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var dataEntity))
        {
            var data = em.GetComponentData<DataContainerComponent>(dataEntity);
            data.selectedSpawnID = spawnId;
            em.SetComponentData(dataEntity, data);
        }

        var heroQuery = em.CreateEntityQuery(ComponentType.ReadOnly<HeroSpawnComponent>(),
                                             ComponentType.ReadOnly<IsLocalPlayer>());
        if (!heroQuery.IsEmptyIgnoreFilter)
        {
            Entity hero = heroQuery.GetSingletonEntity();
            if (em.HasComponent<SpawnSelectionRequest>(hero))
                em.SetComponentData(hero, new SpawnSelectionRequest { spawnId = spawnId });
            else
                em.AddComponent(hero, new SpawnSelectionRequest { spawnId = spawnId });
        }
    }
}
