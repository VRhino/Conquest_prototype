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

            Entity prefabEntity = authoring.data.prefab != null
                ? GetEntity(authoring.data.prefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            AddComponent(entity, new SquadDataComponent
            {
                vidaBase = authoring.data.vidaBase,
                velocidadBase = authoring.data.velocidadBase,
                masa = authoring.data.masa,
                peso = authoring.data.peso,
                squadType = authoring.data.tipo,
                bloqueo = authoring.data.bloqueo,
                defensaCortante = authoring.data.defensaCortante,
                defensaPerforante = authoring.data.defensaPerforante,
                defensaContundente = authoring.data.defensaContundente,
                danoCortante = authoring.data.danoCortante,
                danoPerforante = authoring.data.danoPerforante,
                danoContundente = authoring.data.danoContundente,
                penetracionCortante = authoring.data.penetracionCortante,
                penetracionPerforante = authoring.data.penetracionPerforante,
                penetracionContundente = authoring.data.penetracionContundente,
                esUnidadADistancia = authoring.data.esUnidadADistancia,
                alcance = authoring.data.alcance,
                precision = authoring.data.precision,
                cadenciaFuego = authoring.data.cadenciaFuego,
                velocidadRecarga = authoring.data.velocidadRecarga,
                municionTotal = authoring.data.municionTotal,
                liderazgoCost = authoring.data.liderazgoCost,
                behaviorProfile = authoring.data.behaviorProfile,
                curves = default,
                unitPrefab = prefabEntity,
                unitCount = authoring.data.unitCount
            });

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
