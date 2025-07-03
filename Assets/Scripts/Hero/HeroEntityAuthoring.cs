using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component para crear el prefab ECS puro del héroe (solo lógica, sin visuales).
/// Este se usará para la instanciación de entidades en el EntityManager.
/// </summary>
public class HeroEntityAuthoring : MonoBehaviour
{
    [Header("Hero Configuration")]
    public bool isAlive = true;
    public float deathTimer = 0f;
    public float respawnCooldown = 5f;
    public int spawnId = 1;
    public int teamValue = 1;
    public float baseSpeed = 2f;
    public float sprintMultiplier = 2f;
    public float maxStamina = 100f;
    public float regenRate = 10f;
    public bool startExhausted = false;
    
    [Header("Squad Configuration")]
    public GameObject squadDataPrefab;
    public int instanceId = 0;
    
    [Header("Visual Prefab Reference")]
    public GameObject visualPrefab; // Referencia al prefab visual de Synty
}

/// <summary>
/// Baker que convierte los datos del authoring al conjunto de componentes ECS necesarios.
/// </summary>
public class HeroEntityBaker : Baker<HeroEntityAuthoring>
{
    public override void Bake(HeroEntityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        // Componentes básicos del héroe
        AddComponent(entity, new HeroLifeComponent
        {
            isAlive = authoring.isAlive,
            deathTimer = authoring.deathTimer,
            respawnCooldown = authoring.respawnCooldown
        });
        
        AddComponent(entity, new HeroSpawnComponent
        {
            spawnId = authoring.spawnId,
            spawnPosition = Vector3.zero,
            spawnRotation = quaternion.identity,
            hasSpawned = false
        });
        
        AddComponent(entity, new TeamComponent
        {
            value = (Team)authoring.teamValue
        });
        
        AddComponent(entity, new HeroStatsComponent
        {
            baseSpeed = authoring.baseSpeed,
            sprintMultiplier = authoring.sprintMultiplier
        });
        
        AddComponent(entity, new StaminaComponent
        {
            maxStamina = authoring.maxStamina,
            currentStamina = authoring.maxStamina,
            regenRate = authoring.regenRate,
            isExhausted = authoring.startExhausted
        });
        
        // Squad selection
        if (authoring.squadDataPrefab != null)
        {
            var squadDataEntity = GetEntity(authoring.squadDataPrefab, TransformUsageFlags.None);
            AddComponent(entity, new HeroSquadSelectionComponent
            {
                squadDataEntity = squadDataEntity,
                instanceId = authoring.instanceId
            });
        }
        
        // Componentes de marcado
        AddComponent<IsLocalPlayer>(entity);
        AddComponent<HeroInputComponent>(entity);
        
        // Referencia al prefab visual
        if (authoring.visualPrefab != null)
        {
            AddComponent(entity, new HeroVisualReference
            {
                visualPrefab = GetEntity(authoring.visualPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}
