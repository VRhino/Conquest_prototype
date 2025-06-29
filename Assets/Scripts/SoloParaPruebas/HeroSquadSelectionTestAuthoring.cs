using Unity.Entities;
using UnityEngine;

/// <summary>
/// Script de authoring para pruebas: asigna manualmente el HeroSquadSelectionComponent al prefab del héroe.
/// </summary>
public class HeroSquadSelectionTestAuthoring : MonoBehaviour
{
    [Header("SquadData (GameObject con SquadDataAuthoring)")]
    public GameObject squadDataPrefab;
    [Header("ID de instancia de escuadra (opcional)")]
    public int instanceId = 0;

    class Baker : Baker<HeroSquadSelectionTestAuthoring>
    {
        public override void Bake(HeroSquadSelectionTestAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var squadDataEntity = authoring.squadDataPrefab != null
                ? GetEntity(authoring.squadDataPrefab, TransformUsageFlags.None)
                : Entity.Null;
            AddComponent(entity, new HeroSquadSelectionComponent
            {
                squadDataEntity = squadDataEntity,
                instanceId = authoring.instanceId
            });
        }
    }
}
