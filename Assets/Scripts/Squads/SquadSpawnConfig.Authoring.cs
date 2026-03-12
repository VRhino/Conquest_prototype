using Unity.Entities;
using UnityEngine;

public class SquadSpawnConfigAuthoring : MonoBehaviour
{
    public float squadSpawnOffset = 5f;
    public float unitMinDistance   = 1.5f;
    public float unitRepelForce    = 1f;
    public float unitRotationSpeed = 5f;
    public float heroSlotSpacing   = 10f;

    class Baker : Baker<SquadSpawnConfigAuthoring>
    {
        public override void Bake(SquadSpawnConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SquadSpawnConfigComponent
            {
                squadSpawnOffset  = authoring.squadSpawnOffset,
                unitMinDistance   = authoring.unitMinDistance,
                unitRepelForce    = authoring.unitRepelForce,
                unitRotationSpeed = authoring.unitRotationSpeed,
                heroSlotSpacing   = authoring.heroSlotSpacing
            });
        }
    }
}
