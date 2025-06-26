using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Scales <see cref="UnitStatsComponent"/> values based on squad level.
/// It runs during battle loading and whenever a <see cref="SquadLevelUpEvent"/>
/// is detected.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitStatScalingSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<SquadProgressComponent>();
    }

    protected override void OnUpdate()
    {
        bool applyStats = false;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (_, evtEntity) in SystemAPI
                     .Query<RefRO<SquadLevelUpEvent>>()
                     .WithEntityAccess())
        {
            applyStats = true;
            ecb.DestroyEntity(evtEntity);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();

        if (!applyStats && !IsBattleLoading())
            return;

        var dataLookup = GetComponentLookup<SquadDataComponent>(true);
        var unitBufferLookup = GetBufferLookup<SquadUnitElement>();

        foreach (var (progress, dataRef, squad) in SystemAPI
                     .Query<RefRO<SquadProgressComponent>, RefRO<SquadDataReference>>()
                     .WithEntityAccess())
        {
            if (!dataLookup.TryGetComponent(dataRef.ValueRO.dataEntity, out var data))
                continue;

            ApplyStats(squad, data, progress.ValueRO.level, unitBufferLookup);
        }
    }

    bool IsBattleLoading()
    {
        if (SystemAPI.TryGetSingleton<MatchStateComponent>(out var state))
            return state.currentState == MatchState.LoadingMap;
        return false;
    }

    void ApplyStats(Entity squadEntity,
                    SquadDataComponent data,
                    int level,
                    BufferLookup<SquadUnitElement> unitBufferLookup)
    {
        if (!unitBufferLookup.HasBuffer(squadEntity))
            return;

        int index = math.clamp(level - 1, 0, data.curves.Value.vida.Length - 1);
        float vidaMul = data.curves.Value.vida[index];
        float danoMul = data.curves.Value.dano[index];
        float defMul = data.curves.Value.defensa[index];
        float velMul = data.curves.Value.velocidad[index];

        DynamicBuffer<SquadUnitElement> units = unitBufferLookup[squadEntity];
        foreach (var ue in units)
        {
            if (!EntityManager.Exists(ue.Value))
                continue;

            var stats = new UnitStatsComponent
            {
                vida = data.vidaBase * vidaMul,
                velocidad = data.velocidadBase * velMul,
                masa = data.masa,
                peso = (int)math.round(data.peso),
                bloqueo = data.bloqueo,
                defensaCortante = data.defensaCortante * defMul,
                defensaPerforante = data.defensaPerforante * defMul,
                defensaContundente = data.defensaContundente * defMul,
                danoCortante = data.danoCortante * danoMul,
                danoPerforante = data.danoPerforante * danoMul,
                danoContundente = data.danoContundente * danoMul,
                penetracionCortante = data.penetracionCortante,
                penetracionPerforante = data.penetracionPerforante,
                penetracionContundente = data.penetracionContundente,
                liderazgoCosto = data.liderazgoCost
            };

            if (EntityManager.HasComponent<UnitStatsComponent>(ue.Value))
                EntityManager.SetComponentData(ue.Value, stats);
            else
                EntityManager.AddComponentData(ue.Value, stats);

            if (data.esUnidadADistancia)
            {
                var ranged = new UnitRangedStatsComponent
                {
                    alcance = data.alcance,
                    precision = data.precision,
                    cadenciaFuego = data.cadenciaFuego,
                    velocidadRecarga = data.velocidadRecarga,
                    municionTotal = data.municionTotal
                };
                if (EntityManager.HasComponent<UnitRangedStatsComponent>(ue.Value))
                    EntityManager.SetComponentData(ue.Value, ranged);
                else
                    EntityManager.AddComponentData(ue.Value, ranged);
            }
            else if (EntityManager.HasComponent<UnitRangedStatsComponent>(ue.Value))
            {
                EntityManager.RemoveComponent<UnitRangedStatsComponent>(ue.Value);
            }
        }
    }
}
