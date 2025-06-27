using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake <see cref="SquadData"/> assets into ECS components
/// and buffers.
/// </summary>
public class SquadDataAuthoring : MonoBehaviour
{
    public SquadData data;

    class SquadDataBaker : Unity.Entities.Baker<SquadDataAuthoring>
    {
        public override void Bake(SquadDataAuthoring authoring)
        {
            if (authoring.data == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new SquadStatsComponent
            {
                squadType = authoring.data.tipo,
                behaviorProfile = authoring.data.behaviorProfile
            });

            var statsBuffer = AddBuffer<UnitStatsBufferElement>(entity);
            statsBuffer.Add(new UnitStatsBufferElement
            {
                health = (int)authoring.data.vidaBase,
                speed = (int)authoring.data.velocidadBase,
                mass = (int)authoring.data.masa,
                weightClass = (int)authoring.data.peso,
                blockValue = authoring.data.bloqueo,
                slashingDamage = authoring.data.danoCortante,
                piercingDamage = authoring.data.danoPerforante,
                bluntDamage = authoring.data.danoContundente,
                slashingDefense = authoring.data.defensaCortante,
                piercingDefense = authoring.data.defensaPerforante,
                bluntDefense = authoring.data.defensaContundente,
                leadershipCost = authoring.data.liderazgoCost
            });
        }
    }
}
