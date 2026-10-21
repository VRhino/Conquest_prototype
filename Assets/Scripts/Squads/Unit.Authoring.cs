using Unity.Entities;
using UnityEngine;

namespace Squads
{
    // El UnitAuthoring solo sirve como marcador para el Baker y para exponer parámetros visuales/editoriales.
    public class UnitAuthoring : MonoBehaviour
    {
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
            AddComponent<UnitTargetPositionComponent>(entity);
            // Los stats y datos base se asignan en runtime desde SquadDataComponent
        }
    }
}
