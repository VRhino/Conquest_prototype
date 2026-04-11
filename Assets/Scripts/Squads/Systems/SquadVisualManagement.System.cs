using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using ConquestTactics.Visual;
using System.Collections.Generic;

/// <summary>
/// Sistema que gestiona la instanciación y sincronización de los GameObjects visuales
/// para las unidades de los squads. Los squads son entidades lógicas sin visual propio,
/// solo las unidades individuales tienen representación visual.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadSpawningSystem))]
public partial class SquadVisualManagementSystem : SystemBase
{
    private const string DetectorChildName    = "Detector";
    private const string WeaponHitboxChildName = "WeaponHitbox";
    private static readonly int ShaderBaseColor = Shader.PropertyToID("_BaseColor");

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var pendingNavAgents = new List<(Entity entity, NavMeshAgent agent)>();

        // Solo crear visuales para unidades individuales
        // Los squads son entidades lógicas sin visual propio
        CreateUnitVisuals(ecb, pendingNavAgents);

        ecb.Playback(EntityManager);
        ecb.Dispose();

        // Adjuntar NavMeshAgent como componente manejado después del playback para evitar
        // cambios estructurales durante la iteración de queries
        foreach (var (entity, agent) in pendingNavAgents)
        {
            EntityManager.AddComponentObject(entity, agent);
            EntityManager.AddComponentData(entity, new NavAgentComponent { syncPositionFromNavMesh = true });
        }
    }

    /// <summary>
    /// Crea visuales para unidades que no tienen visual todavía.
    /// Los squads no tienen visuales propios, solo las unidades individuales.
    /// </summary>
    private void CreateUnitVisuals(EntityCommandBuffer ecb, List<(Entity, NavMeshAgent)> pendingNavAgents)
    {
        foreach (var (unitVisualRef, transform, entity) in
                 SystemAPI.Query<RefRO<UnitVisualReference>,
                                 RefRO<LocalTransform>>()
                        .WithNone<UnitVisualInstance>()
                        .WithEntityAccess())
        {
            CreateVisualForUnit(entity, unitVisualRef.ValueRO, transform.ValueRO, ecb, pendingNavAgents);
        }
    }
    
    /// <summary>
    /// Crea un GameObject visual para la unidad especificada.
    /// </summary>
    /// <param name="unitEntity">Entidad de la unidad</param>
    /// <param name="visualRef">Referencia al prefab visual</param>
    /// <param name="transform">Transform inicial de la unidad</param>
    /// <param name="ecb">EntityCommandBuffer para agregar componentes</param>
    private void CreateVisualForUnit(Entity unitEntity, UnitVisualReference visualRef,
        LocalTransform transform, EntityCommandBuffer ecb, List<(Entity, NavMeshAgent)> pendingNavAgents)
    {
        // Buscar el squad padre para determinar el tipo
        Entity parentSquad = FindParentSquad(unitEntity);
        SquadType squadType = SquadType.Squires; // Default
        
        if (parentSquad != Entity.Null && EntityManager.HasComponent<SquadDefinitionComponent>(parentSquad))
        {
            var squadDef = EntityManager.GetComponentData<SquadDefinitionComponent>(parentSquad);
            squadType = squadDef.squadType;
        }
        
        // Buscar el prefab visual
        GameObject visualPrefab = FindUnitVisualPrefab(visualRef.visualPrefabName.ToString(), squadType);
        
        if (visualPrefab == null)
        {
            Debug.LogWarning($"[SquadVisualManagementSystem] Prefab visual de unidad no encontrado: {visualRef.visualPrefabName}");
            return;
        }
        
        // Instanciar el GameObject visual de la unidad
        GameObject visualInstance = Object.Instantiate(visualPrefab);
        visualInstance.transform.position = transform.Position;
        visualInstance.transform.rotation = transform.Rotation;
        visualInstance.name = $"Unit_{unitEntity.Index}_Visual";

        // Asignar layer "Units" para culling de distancia nativo por cámara
        int unitsLayer = LayerMask.NameToLayer("Units");
        int targetLayer = unitsLayer >= 0 ? unitsLayer : 0;
        SetLayerRecursively(visualInstance, targetLayer);

        var syncScript = VisualSyncUtility.SetupVisualSync(visualInstance);
        syncScript.SetHeroEntity(unitEntity);

        ApplyDetectorColor(visualInstance, unitEntity, parentSquad);

        // Wire WeaponHitboxBehaviour to its owner ECS entity (placed by designer in prefab)
        var hitboxBehaviour = visualInstance.GetComponentInChildren<WeaponHitboxBehaviour>(true);
        if (hitboxBehaviour != null)
        {
            hitboxBehaviour.ownerUnit = unitEntity;

        }
        else
        {

        }

        // Wire ShieldHitboxBehaviour → add UnitShieldComponent if prefab has shield
        var shieldBehaviour = visualInstance.GetComponentInChildren<ShieldHitboxBehaviour>(true);
        if (shieldBehaviour != null)
        {
            shieldBehaviour.ownerUnit = unitEntity;

            // Resolve shield values: prefab override > SquadData > config defaults
            var shieldCfg = SystemAPI.GetSingleton<ShieldConfigComponent>();
            float maxBlock = shieldBehaviour.maxBlockOverride > 0f
                ? shieldBehaviour.maxBlockOverride
                : shieldCfg.defaultMaxBlock;
            float regenRate = shieldBehaviour.regenRateOverride > 0f
                ? shieldBehaviour.regenRateOverride
                : shieldCfg.defaultRegenRate;

            if (parentSquad != Entity.Null && EntityManager.HasComponent<SquadDataComponent>(parentSquad))
            {
                var squadData = EntityManager.GetComponentData<SquadDataComponent>(parentSquad);
                if (shieldBehaviour.maxBlockOverride <= 0f && squadData.block > 0f)
                    maxBlock = squadData.block;
                if (shieldBehaviour.regenRateOverride <= 0f && squadData.blockRegenRate > 0f)
                    regenRate = squadData.blockRegenRate;
            }

            float breakStunDuration = shieldCfg.defaultBreakStunDuration;
            if (parentSquad != Entity.Null && EntityManager.HasComponent<SquadDataComponent>(parentSquad))
            {
                var squadData2 = EntityManager.GetComponentData<SquadDataComponent>(parentSquad);
                if (squadData2.shieldBreakStunDuration > 0f)
                    breakStunDuration = squadData2.shieldBreakStunDuration;
            }

            ecb.AddComponent(unitEntity, new UnitShieldComponent
            {
                currentBlock      = maxBlock,
                maxBlock          = maxBlock,
                regenRate         = regenRate,
                orientation       = shieldBehaviour.orientation,
                breakStunDuration = breakStunDuration
            });
        }

        var agent = visualInstance.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(visualInstance.transform.position); // fuerza posición al spawn, evita warp al NavMesh más cercano
            pendingNavAgents.Add((unitEntity, agent));
        }

        // Marcar la unidad como teniendo visual
        ecb.AddComponent(unitEntity, new UnitVisualInstance
        {
            visualInstanceId = visualInstance.GetInstanceID(),
            parentSquad = parentSquad
        });
        
        // Visual de unidad creado
    }
    
    /// <summary>
    /// Busca el squad padre de una unidad.
    /// </summary>
    /// <param name="unitEntity">Entidad de la unidad</param>
    /// <returns>Entidad del squad padre o Entity.Null</returns>
    private Entity FindParentSquad(Entity unitEntity)
    {
        // Buscar en todos los squads para encontrar el que contiene esta unidad
        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].Value == unitEntity)
                {
                    return squadEntity;
                }
            }
        }
        
        return Entity.Null;
    }
    
    /// <summary>
    /// Busca el prefab visual de la unidad usando el VisualPrefabRegistry.
    /// </summary>
    /// <param name="prefabName">Nombre del prefab</param>
    /// <param name="squadType">Tipo de squad para determinar el tipo de unidad</param>
    /// <returns>GameObject del prefab visual o null</returns>
    private GameObject FindUnitVisualPrefab(string prefabName, SquadType squadType)
    {
        var registry = VisualPrefabRegistry.Instance;
        // Intentar por nombre específico primero
        if (!string.IsNullOrEmpty(prefabName))
        {
            GameObject prefab = registry.GetPrefab(prefabName);
            if (prefab != null) return prefab;
        }
        
        // Fallback al tipo de squad
        return registry.GetDefaultUnitPrefab(squadType);
    }

    private void ApplyDetectorColor(GameObject visualInstance, Entity unitEntity, Entity parentSquad)
    {
        Transform detectorTransform = visualInstance.transform.Find(DetectorChildName);
        if (detectorTransform == null) return;

        Renderer cubeRenderer = detectorTransform.GetComponentInChildren<Renderer>();
        if (cubeRenderer == null) return;

        bool isLocalSquad = parentSquad != Entity.Null &&
                            EntityManager.HasComponent<IsLocalSquadActive>(parentSquad);

        Color color;
        if (isLocalSquad)
        {
            color = Color.yellow;
        }
        else if (SystemAPI.TryGetSingleton<DataContainerComponent>(out var dc) &&
                 EntityManager.HasComponent<TeamComponent>(unitEntity))
        {
            var unitTeam = EntityManager.GetComponentData<TeamComponent>(unitEntity).value;
            color = (unitTeam == (Team)dc.teamID) ? Color.blue : Color.red;
        }
        else
        {
            color = Color.white;
        }

        var mpb = new MaterialPropertyBlock();
        cubeRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(ShaderBaseColor, color);
        cubeRenderer.SetPropertyBlock(mpb);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}