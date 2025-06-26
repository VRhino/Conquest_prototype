using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Handles experience gain and level progression for all active squads.
/// Updates unit stats and unlockable content when a level is gained.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadProgressionSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<SquadProgressComponent>();
    }

    protected override void OnUpdate()
    {
        if (!IsPostMatch())
            return;

        var dataLookup = GetComponentLookup<SquadDataComponent>(true);
        var abilityLookup = GetBufferLookup<AbilityByLevelElement>(true);
        var formationLookup = GetBufferLookup<AvailableFormationElement>(true);
        var unitBufferLookup = GetBufferLookup<SquadUnitElement>(true);

        foreach (var (progress, dataRef, entity) in SystemAPI
                     .Query<RefRW<SquadProgressComponent>, RefRO<SquadDataReference>>()
                     .WithEntityAccess())
        {
            if (!dataLookup.TryGetComponent(dataRef.ValueRO.dataEntity, out var data))
                continue;

            float xpGain = 10f;
            if (SystemAPI.HasComponent<SquadStateComponent>(entity) &&
                SystemAPI.GetComponent<SquadStateComponent>(entity).isInCombat)
                xpGain = 50f;
            progress.ValueRW.currentXP += xpGain;

            while (progress.ValueRO.currentXP >= progress.ValueRO.xpToNextLevel && progress.ValueRW.level < 30)
            {
                progress.ValueRW.currentXP -= progress.ValueRO.xpToNextLevel;
                progress.ValueRW.level += 1;
                progress.ValueRW.xpToNextLevel = CalculateNext(progress.ValueRO.level);

                ApplyStats(entity, data, progress.ValueRO.level, unitBufferLookup);
                UnlockAbility(entity, dataRef.ValueRO.dataEntity, progress.ValueRO.level, abilityLookup);
                UnlockFormation(entity, dataRef.ValueRO.dataEntity, progress.ValueRO.level, formationLookup);
            }
        }
    }

    static float CalculateNext(int level)
    {
        return math.floor(100f * math.pow(1.1f, level - 1));
    }

    bool IsPostMatch()
    {
        if (SystemAPI.TryGetSingleton<GameStateComponent>(out var state))
            return state.currentPhase == GamePhase.PostPartida;
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
                peso = data.peso,
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
        }
    }

    void UnlockAbility(Entity squadEntity, Entity dataEntity, int level,
                       BufferLookup<AbilityByLevelElement> abilityLookup)
    {
        if (!abilityLookup.HasBuffer(dataEntity) || level % 10 != 0)
            return;

        var abilities = abilityLookup[dataEntity];
        int index = level / 10 - 1;
        if (index < 0 || index >= abilities.Length)
            return;

        if (!EntityManager.HasBuffer<UnlockedAbilityElement>(squadEntity))
            EntityManager.AddBuffer<UnlockedAbilityElement>(squadEntity);
        var unlocked = EntityManager.GetBuffer<UnlockedAbilityElement>(squadEntity);
        Entity ability = abilities[index].Value;
        foreach (var a in unlocked)
            if (a.Value == ability)
                return;
        unlocked.Add(new UnlockedAbilityElement { Value = ability });
    }

    void UnlockFormation(Entity squadEntity, Entity dataEntity, int level,
                         BufferLookup<AvailableFormationElement> formationLookup)
    {
        if (!formationLookup.HasBuffer(dataEntity))
            return;
        var forms = formationLookup[dataEntity];
        int index = level / 10; // first formation assumed available at level 1
        if (index < 0 || index >= forms.Length)
            return;

        if (!EntityManager.HasBuffer<UnlockedFormationElement>(squadEntity))
            EntityManager.AddBuffer<UnlockedFormationElement>(squadEntity);
        var unlocked = EntityManager.GetBuffer<UnlockedFormationElement>(squadEntity);
        var form = forms[index].Value;
        foreach (var f in unlocked)
            if (f.Value == form)
                return;
        unlocked.Add(new UnlockedFormationElement { Value = form });
    }
}

