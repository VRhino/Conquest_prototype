using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Validates equipment status for squad units before combat. It updates the
/// <see cref="UnitEquipmentComponent"/> values and emits warnings for the HUD.
/// Squads marked as <see cref="NonDeployableSquadTag"/> are ignored by
/// <c>LoadoutSystem</c> when composing the player's loadout.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitDeploymentValidationSystem : SystemBase
{
    LocalSaveSystem.PlayerProgressData _progress;
    bool _initialized;

    protected override void OnCreate()
    {
        base.OnCreate();
        _progress = LocalSaveSystem.LoadProgress();
    }

    protected override void OnUpdate()
    {
        if (!_initialized)
        {
            InitializeEquipment();
            _initialized = true;
        }

        if (IsPostMatch())
        {
            SaveEquipment();
            return;
        }

        if (!IsPreparationPhase()) return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (equip, entity) in SystemAPI
                     .Query<RefRW<UnitEquipmentComponent>>()
                     .WithEntityAccess())
        {
            var data = equip.ValueRW;
            bool changed = false;

            if (data.armorPercent < 50f && !data.hasDebuff)
            {
                data.hasDebuff = true;
                changed = true;
            }
            else if (data.armorPercent >= 50f && data.hasDebuff)
            {
                data.hasDebuff = false;
                changed = true;
            }

            if (data.armorPercent <= 0f && data.isDeployable)
            {
                data.isDeployable = false;
                if (!SystemAPI.HasComponent<NonDeployableSquadTag>(entity))
                    ecb.AddComponent<NonDeployableSquadTag>(entity);
                changed = true;
            }
            else if (data.armorPercent > 0f && !data.isDeployable)
            {
                data.isDeployable = true;
                if (SystemAPI.HasComponent<NonDeployableSquadTag>(entity))
                    ecb.RemoveComponent<NonDeployableSquadTag>(entity);
                changed = true;
            }

            if (changed)
            {
                Entity evt = ecb.CreateEntity();
                ecb.AddComponent(evt, new SquadDeploymentWarningEvent
                {
                    squad = entity,
                    isDeployable = data.isDeployable,
                    hasDebuff = data.hasDebuff
                });
            }

            equip.ValueRW = data;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    void InitializeEquipment()
    {
        var dataLookup = GetComponentLookup<SquadDataComponent>(true);
        foreach (var (equip, dataRef, instance) in SystemAPI
                     .Query<RefRW<UnitEquipmentComponent>,
                            RefRO<SquadDataReference>,
                            RefRO<SquadInstanceComponent>>())
        {
            if (!dataLookup.TryGetComponent(dataRef.ValueRO.dataEntity, out var data))
                continue;

            LocalSaveSystem.SquadInstanceData record = null;
            foreach (var r in _progress.squads)
                if (r.id == instance.ValueRO.id)
                {
                    record = r;
                    break;
                }
            if (record != null)
            {
                equip.ValueRW.armorPercent = math.clamp(record.armorPercent, 0f, 100f);
                equip.ValueRW.hasDebuff = equip.ValueRW.armorPercent < 50f;
                equip.ValueRW.isDeployable = equip.ValueRW.armorPercent > 0f;
            }
            else
            {
                equip.ValueRW.armorPercent = 100f;
                equip.ValueRW.hasDebuff = false;
                equip.ValueRW.isDeployable = true;
            }
        }
    }

    void SaveEquipment()
    {
        var dataLookup = GetComponentLookup<SquadDataComponent>(true);
        bool save = false;

        var deadLookup = GetComponentLookup<IsDeadComponent>(true);
        foreach (var (equip, dataRef, instance, units) in SystemAPI
                     .Query<RefRW<UnitEquipmentComponent>,
                            RefRO<SquadDataReference>,
                            RefRO<SquadInstanceComponent>,
                            DynamicBuffer<SquadUnitElement>>())
        {
            if (!dataLookup.TryGetComponent(dataRef.ValueRO.dataEntity, out var data))
                continue;

            LocalSaveSystem.SquadInstanceData record = null;
            foreach (var r in _progress.squads)
                if (r.id == instance.ValueRO.id)
                {
                    record = r;
                    break;
                }
            float total = units.Length;
            int alive = 0;
            for (int i = 0; i < units.Length; i++)
            {
                Entity u = units[i].Value;
                if (SystemAPI.Exists(u) && !deadLookup.HasComponent(u))
                    alive++;
            }

            float newPercent = total > 0 ? (alive / total) * 100f : 0f;
            equip.ValueRW.armorPercent = newPercent;
            equip.ValueRW.hasDebuff = newPercent < 50f;
            equip.ValueRW.isDeployable = newPercent > 0f;

            if (record == null)
            {
                _progress.squads.Add(new LocalSaveSystem.SquadInstanceData
                {
                    id = instance.ValueRO.id,
                    squadType = data.squadType,
                    armorPercent = newPercent
                });
                save = true;
            }
            else if (math.abs(record.armorPercent - newPercent) > 0.01f)
            {
                record.armorPercent = newPercent;
                save = true;
            }
        }

        if (save)
            LocalSaveSystem.SaveProgress(_progress);
    }

    bool IsPreparationPhase()
    {
        var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (q.IsEmptyIgnoreFilter)
            return false;
        var state = q.GetSingleton<GameStateComponent>();
        return state.currentPhase == GamePhase.Preparacion;
    }

    bool IsPostMatch()
    {
        var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (q.IsEmptyIgnoreFilter)
            return false;
        var state = q.GetSingleton<GameStateComponent>();
        return state.currentPhase == GamePhase.PostPartida;
    }
}
