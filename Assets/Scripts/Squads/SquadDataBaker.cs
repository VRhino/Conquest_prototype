using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Baker that converts <see cref="SquadData"/> assets into entities containing
/// a <see cref="SquadDataComponent"/> and buffers for abilities and formations.
/// </summary>
public class SquadDataBaker : Baker<SquadData>
{
    public override void Bake(SquadData authoring)
    {
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
            vidaArr[i] = authoring.vidaCurve.Evaluate(level);
            danoArr[i] = authoring.danoCurve.Evaluate(level);
            defensaArr[i] = authoring.defensaCurve.Evaluate(level);
            velArr[i] = authoring.velocidadCurve.Evaluate(level);
        }
        var blob = builder.CreateBlobAssetReference<SquadProgressionCurveBlob>(Allocator.Persistent);
        builder.Dispose();

        AddComponent(entity, new SquadDataComponent
        {
            vidaBase = authoring.vidaBase,
            velocidadBase = authoring.velocidadBase,
            masa = authoring.masa,
            peso = authoring.peso,
            squadType = authoring.tipo,
            bloqueo = authoring.bloqueo,
            defensaCortante = authoring.defensaCortante,
            defensaPerforante = authoring.defensaPerforante,
            defensaContundente = authoring.defensaContundente,
            danoCortante = authoring.danoCortante,
            danoPerforante = authoring.danoPerforante,
            danoContundente = authoring.danoContundente,
            penetracionCortante = authoring.penetracionCortante,
            penetracionPerforante = authoring.penetracionPerforante,
            penetracionContundente = authoring.penetracionContundente,
            esUnidadADistancia = authoring.esUnidadADistancia,
            alcance = authoring.alcance,
            precision = authoring.precision,
            cadenciaFuego = authoring.cadenciaFuego,
            velocidadRecarga = authoring.velocidadRecarga,
            municionTotal = authoring.municionTotal,
            liderazgoCost = authoring.liderazgoCost,
            behaviorProfile = authoring.behaviorProfile,
            curves = blob
        });

        var abilityBuffer = AddBuffer<AbilityByLevelElement>(entity);
        if (authoring.abilitiesByLevel != null)
        {
            foreach (var ability in authoring.abilitiesByLevel)
            {
                if (ability != null)
                    abilityBuffer.Add(new AbilityByLevelElement { Value = GetEntity(ability, TransformUsageFlags.None) });
            }
        }

        var formationBuffer = AddBuffer<AvailableFormationElement>(entity);
        if (authoring.availableFormations != null)
        {
            foreach (var form in authoring.availableFormations)
                formationBuffer.Add(new AvailableFormationElement { Value = form });
        }
    }
}

