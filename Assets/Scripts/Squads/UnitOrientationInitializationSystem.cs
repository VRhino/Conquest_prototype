using Unity.Entities;

/// <summary>
/// Sistema que inicializa automáticamente el componente de orientación 
/// para unidades de squad que no lo tengan configurado.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class UnitOrientationInitializationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        
        // Buscar unidades que no tengan orientación
        var entities = SystemAPI.QueryBuilder()
            .WithNone<UnitOrientationComponent>()
            .Build()
            .ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            // Agregar orientación por defecto: mirar hacia el héroe
            ecb.AddComponent(entity, new UnitOrientationComponent
            {
                orientationType = UnitOrientationType.FaceHero,
                rotationSpeed = 5f
            });
        }
        entities.Dispose();
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
