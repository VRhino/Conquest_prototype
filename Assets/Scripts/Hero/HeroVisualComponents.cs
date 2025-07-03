using Unity.Entities;

/// <summary>
/// Componente que almacena la referencia al prefab visual del héroe.
/// Se usa para instanciar el GameObject visual cuando se crea la entidad.
/// </summary>
public struct HeroVisualReference : IComponentData
{
    /// <summary>Prefab visual del héroe (GameObject de Synty).</summary>
    public Entity visualPrefab;
}

/// <summary>
/// Componente que vincula una entidad con su GameObject visual instanciado.
/// </summary>
public struct HeroVisualInstance : IComponentData
{
    /// <summary>ID del GameObject visual asociado a esta entidad.</summary>
    public int visualInstanceId;
}
