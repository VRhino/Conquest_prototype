using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Validates hero attribute assignments against <see cref="HeroClassDefinition"/>
/// limits. Runs only outside of combat.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroAttributeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Skip validation while in combat.
        if (SystemAPI.TryGetSingleton<GameStateComponent>(out var state) &&
            state.currentPhase == GamePhase.Combate)
            return;

        var defLookup = GetComponentLookup<HeroClassDefinitionComponent>(true);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (attr, entity) in SystemAPI
                     .Query<RefRW<HeroAttributesComponent>>()
                     .WithEntityAccess())
        {
            if (!defLookup.TryGetComponent(attr.ValueRO.classDefinition, out var def))
                continue;

            var data = attr.ValueRW;
            bool changed = false;

            changed |= Validate(ref data.fuerza, def.minFuerza, def.maxFuerza,
                                HeroAttributeType.Strength, entity, ref ecb);
            changed |= Validate(ref data.destreza, def.minDestreza, def.maxDestreza,
                                HeroAttributeType.Dexterity, entity, ref ecb);
            changed |= Validate(ref data.armadura, def.minArmadura, def.maxArmadura,
                                HeroAttributeType.Armor, entity, ref ecb);
            changed |= Validate(ref data.vitalidad, def.minVitalidad, def.maxVitalidad,
                                HeroAttributeType.Vitality, entity, ref ecb);

            if (changed)
                attr.ValueRW = data;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    static bool Validate(ref int value, int min, int max, HeroAttributeType type,
                          Entity target, ref EntityCommandBuffer ecb)
    {
        if (value < min)
        {
            ecb.AddComponent(target, new AttributeLimitEvent
            {
                attribute = type,
                attemptedValue = value
            });
            value = min;
            return true;
        }
        if (value > max)
        {
            ecb.AddComponent(target, new AttributeLimitEvent
            {
                attribute = type,
                attemptedValue = value
            });
            value = max;
            return true;
        }
        return false;
    }
}

