using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Componentes para el manejo de visuales de unidades.
/// Los squads son entidades lógicas sin visual propio, solo las unidades tienen visuales.
/// </summary>

/// <summary>
/// Componente que almacena una referencia al prefab visual que debe
/// instanciarse para esta unidad ECS.
/// </summary>
public struct UnitVisualReference : IComponentData
{
    /// <summary>Nombre del prefab visual a instanciar (buscar en registry)</summary>
    public FixedString64Bytes visualPrefabName;
    
    /// <summary>Entidad del prefab visual (opcional, para referencia directa)</summary>
    public Entity visualPrefab;
}

/// <summary>
/// Componente que marca que una unidad ya tiene una instancia visual creada
/// y almacena información sobre ella.
/// </summary>
public struct UnitVisualInstance : IComponentData
{
    /// <summary>ID de instancia del GameObject visual creado</summary>
    public int visualInstanceId;
    
    /// <summary>Referencia al squad al que pertenece esta unidad</summary>
    public Entity parentSquad;
}
