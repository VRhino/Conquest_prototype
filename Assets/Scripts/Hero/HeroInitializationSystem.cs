using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Initializes hero attributes, abilities and perk lists when a hero is
/// created or loaded from saved progression.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class HeroInitializationSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<HeroClassReference>();
    }

    protected override void OnUpdate()
    {
        var defLookup = GetComponentLookup<HeroClassDefinitionComponent>(true);
        var perkLookup = GetBufferLookup<ValidPerkElement>(true);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (classRef, entity) in SystemAPI
                     .Query<RefRO<HeroClassReference>>()
                     .WithNone<HeroAttributesComponent>()
                     .WithEntityAccess())
        {
            if (!defLookup.TryGetComponent(classRef.ValueRO.classEntity, out var def))
                continue;

            ecb.AddComponent(entity, new HeroAttributesComponent
            {
                fuerza = def.baseFuerza,
                destreza = def.baseDestreza,
                armadura = def.baseArmadura,
                vitalidad = def.baseVitalidad,
                classDefinition = classRef.ValueRO.classEntity
            });

            ecb.AddComponent(entity, new HeroAbilityComponent
            {
                abilityQ = def.abilityQ,
                abilityE = def.abilityE,
                abilityR = def.abilityR,
                ultimate = def.ultimate
            });

            if (perkLookup.HasBuffer(classRef.ValueRO.classEntity))
            {
                var source = perkLookup[classRef.ValueRO.classEntity];
                var dest = ecb.AddBuffer<ValidPerkElement>(entity);
                foreach (var perk in source)
                    dest.Add(perk);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
