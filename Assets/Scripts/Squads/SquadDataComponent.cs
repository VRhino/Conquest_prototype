using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Runtime representation of <see cref="SquadData"/> used by squad systems.
/// </summary>
public struct SquadDataComponent : IComponentData
{
    public float vidaBase;
    public float velocidadBase;
    public float masa;
    public float peso;
    /// <summary>Classification of squad units.</summary>
    public SquadType squadType;
    public float bloqueo;

    public float defensaCortante;
    public float defensaPerforante;
    public float defensaContundente;

    public float danoCortante;
    public float danoPerforante;
    public float danoContundente;

    public float penetracionCortante;
    public float penetracionPerforante;
    public float penetracionContundente;

    public bool esUnidadADistancia;
    public float alcance;
    public float precision;
    public float cadenciaFuego;
    public float velocidadRecarga;
    public int municionTotal;

    public int liderazgoCost;
    public BehaviorProfile behaviorProfile;

    /// <summary>Progression curves sampled per level.</summary>
    public BlobAssetReference<SquadProgressionCurveBlob> curves;

    /// <summary>Formation patterns available to this squad type.</summary>
    public BlobAssetReference<FormationLibraryBlob> formationLibrary;

    /// <summary>Prefab entity used to instantiate squad units.</summary>
    public Entity unitPrefab;

    public int unitCount;
    /// <summary>Tamaño de la grid de formación (celdas en X, celdas en Y)</summary>
    public int2 GridSize;
}

/// <summary>
/// Buffer element mapping abilities to the level they unlock every 10 levels.
/// </summary>
public struct AbilityByLevelElement : IBufferElementData
{
    public Entity Value;
}

/// <summary>
/// Component used on squad entities to reference their <see cref="SquadDataComponent"/> entity.
/// </summary>
public struct SquadDataReference : IComponentData
{
    public Entity dataEntity;
}


