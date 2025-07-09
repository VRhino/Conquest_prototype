using Unity.Entities;
using UnityEngine;

namespace Squads
{
    // El UnitAuthoring solo sirve como marcador para el Baker y para exponer parámetros visuales/editoriales.
    public class UnitAuthoring : MonoBehaviour
    {
        [Header("Espaciado (solo visual/editor)")]
        public float minDistance = 1.5f;
        public float repelForce = 1f;
    }

    public class UnitBaker : Baker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Solo se agregan componentes que no dependen de SquadData
            AddComponent(entity, new UnitEquipmentComponent
            {
                armorPercent = 100f,
                hasDebuff = false,
                isDeployable = true
            });

            AddComponent<UnitCombatComponent>(entity);
            AddComponent(entity, new UnitSpacingComponent
            {
                minDistance = authoring.minDistance,
                repelForce = authoring.repelForce
            });
            AddComponent<UnitTargetPositionComponent>(entity);
            // Los stats y datos base se asignan en runtime desde SquadDataComponent
        }
    }
}
