using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ConquestTactics.Animation;

/// <summary>
/// Sistema ECS que actualiza el componente UnitAnimationMovementComponent
/// para cada unidad basado en su movimiento físico.
/// 
/// Este sistema se ejecuta después del UnitFollowFormationSystem para tener
/// información de movimiento actualizada.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFollowFormationSystem))]
[UpdateAfter(typeof(SquadSpawningSystem))]
[UpdateAfter(typeof(NavMeshPositionSyncSystem))]
[UpdateAfter(typeof(UnitRotationResolutionSystem))]
public partial class UnitAnimationSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        RequireForUpdate<SquadSpawnConfigComponent>();
    }

    protected override void OnUpdate()
    {
        // Delta time para calcular velocidad
        float dt = SystemAPI.Time.DeltaTime;
        if (dt <= 0f) return;
        var config = SystemAPI.GetSingleton<SquadSpawnConfigComponent>();
        
        // Lookup para componentes
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        var statsLookup = GetComponentLookup<UnitStatsComponent>(true);
        
        // Para cada unidad con componente de animación, actualizar datos de movimiento
        foreach (var (animComponent, entity) in 
                 SystemAPI.Query<RefRW<UnitAnimationMovementComponent>>()
                 .WithNone<IsDeadComponent>()
                 .WithEntityAccess())
        {
            if (!transformLookup.HasComponent(entity))
                continue;
                
            // Posición actual
            float3 currentPos = transformLookup[entity].Position;
            
            // Determinar posición anterior usando el campo per-unit
            float3 prevPos = animComponent.ValueRO.PreviousPosition;
            
            // Calcular vector de movimiento y velocidad
            float3 movementVector = currentPos - prevPos;
            movementVector.y = 0f;
            float speedMagnitude = math.length(movementVector) / dt;
            
            // Obtener velocidad máxima de las stats de la unidad o usar un valor por defecto
            float maxSpeed = 0f;
            if (statsLookup.HasComponent(entity))
            {
                maxSpeed = statsLookup[entity].speed;
            }
            
            float requestedSpeed = maxSpeed;
            if (EntityManager.HasComponent<UnityEngine.AI.NavMeshAgent>(entity))
            {
                var agent = EntityManager.GetComponentObject<UnityEngine.AI.NavMeshAgent>(entity);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    var velocity = agent.velocity;
                    movementVector = new float3(velocity.x, 0f, velocity.z);
                    speedMagnitude = math.length(movementVector);
                    requestedSpeed = agent.speed;
                }
                else
                {
                    movementVector = float3.zero;
                    speedMagnitude = 0f;
                }
            }
            bool isMoving = speedMagnitude > config.animationMovingSpeedThreshold;
            
            // Actualizar los contadores de tiempo para transiciones suaves
            if (isMoving)
            {
                animComponent.ValueRW.MovementTime += dt;
                animComponent.ValueRW.StoppedTime = 0f;
            }
            else
            {
                animComponent.ValueRW.MovementTime = 0f;
                animComponent.ValueRW.StoppedTime += dt;
            }
            
            // Normalizar la dirección de movimiento si está en movimiento
            float3 movementDirection = isMoving ? math.normalizesafe(movementVector) : float3.zero;
            
            // Determinar si está corriendo (más del 60% de su velocidad máxima)
            bool isRunning = isMoving && speedMagnitude > maxSpeed * config.animationRunningSpeedRatio;
            
            // Actualizar todos los valores del componente
            animComponent.ValueRW.CurrentSpeed = speedMagnitude;
            animComponent.ValueRW.MaxSpeed = maxSpeed;
            animComponent.ValueRW.RequestedSpeed = requestedSpeed;
            animComponent.ValueRW.MovementDirection = movementDirection;
            animComponent.ValueRW.IsMoving = isMoving;
            animComponent.ValueRW.IsRunning = isRunning;
            

            // Actualizar PreviousPosition para el siguiente frame
            animComponent.ValueRW.PreviousPosition = currentPos;
        }
    }
}
