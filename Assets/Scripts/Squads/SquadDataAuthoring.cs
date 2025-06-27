using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake <see cref="SquadData"/> assets into entities containing
/// a <see cref="SquadDataComponent"/> and buffers for abilities and formations.
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

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SquadProgressionCurveBlob>();
            var vidaArr = builder.Allocate(ref root.vida, 30);
            var danoArr = builder.Allocate(ref root.dano, 30);
            var defensaArr = builder.Allocate(ref root.defensa, 30);
            var velArr = builder.Allocate(ref root.velocidad, 30);
            for (int i = 0; i < 30; i++)
            {
                int level = i + 1;
                vidaArr[i] = authoring.data.vidaCurve.Evaluate(level);
                danoArr[i] = authoring.data.danoCurve.Evaluate(level);
                defensaArr[i] = authoring.data.defensaCurve.Evaluate(level);
                velArr[i] = authoring.data.velocidadCurve.Evaluate(level);
            }
            var blob = builder.CreateBlobAssetReference<SquadProgressionCurveBlob>(Allocator.Persistent);
            builder.Dispose();

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
                curves = blob
            });

            var abilityBuffer = AddBuffer<AbilityByLevelElement>(entity);
            if (authoring.data.abilitiesByLevel != null)
            {
                foreach (var ability in authoring.data.abilitiesByLevel)
                {
                    if (ability != null)
                        abilityBuffer.Add(new AbilityByLevelElement { Value = GetEntity(ability, TransformUsageFlags.None) });
                }
            }

            var formationBuffer = AddBuffer<AvailableFormationElement>(entity);
            if (authoring.data.availableFormations != null)
            {
                foreach (var form in authoring.data.availableFormations)
                    formationBuffer.Add(new AvailableFormationElement { Value = form });
            }
        }
    }
}
