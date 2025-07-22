using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component for destination marker prefab configuration.
/// Add this to a GameObject in your scene to configure the marker prefab.
/// </summary>
public class DestinationMarkerAuthoring : MonoBehaviour
{
    [Header("Destination Marker Settings")]
    [SerializeField] private GameObject markerPrefab;
    
    public GameObject MarkerPrefab => markerPrefab;
}

/// <summary>
/// Baker for DestinationMarkerAuthoring component.
/// </summary>
public class DestinationMarkerBaker : Baker<DestinationMarkerAuthoring>
{
    public override void Bake(DestinationMarkerAuthoring authoring)
    {
        if (authoring.MarkerPrefab != null)
        {
            // Create the singleton entity
            var entity = GetEntity(TransformUsageFlags.None);
            
            // Get the prefab entity
            var prefabEntity = GetEntity(authoring.MarkerPrefab, TransformUsageFlags.Dynamic);
            
            // Add the component with the prefab reference
            AddComponent(entity, new DestinationMarkerPrefabComponent
            {
                markerPrefab = prefabEntity
            });
        }
    }
}
