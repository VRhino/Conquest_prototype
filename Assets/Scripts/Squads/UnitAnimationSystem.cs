using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
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
[UpdateAfter(typeof(HeroMovementSystem))]
[UpdateAfter(typeof(SquadSpawningSystem))]
public partial class UnitAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Delta time para calcular velocidad
        float dt = SystemAPI.Time.DeltaTime;
        if (dt < 0.0001f) return; // Evitar división por 0
        
        // Lookup para componentes
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        var prevPosLookup = GetComponentLookup<UnitPrevLeaderPosComponent>(true);
        var statsLookup = GetComponentLookup<UnitStatsComponent>(true);
        
        // Para cada unidad con componente de animación, actualizar datos de movimiento
        foreach (var (animComponent, entity) in 
                 SystemAPI.Query<RefRW<UnitAnimationMovementComponent>>()
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
            float speedMagnitude = math.length(movementVector) / dt;
            
            // Verificación adicional de movimiento basada en la posición directamente
            // Esto ayuda a detectar movimiento incluso con valores muy pequeños
            float positionDifference = math.distance(currentPos, prevPos);
            
            // Obtener velocidad máxima de las stats de la unidad o usar un valor por defecto
            float maxSpeed = 5f; // Velocidad base por defecto
            if (statsLookup.HasComponent(entity))
            {
                maxSpeed = statsLookup[entity].velocidad;
            }
            
            // Umbral más pequeño para detectar movimiento
            float movementThreshold = 0.001f;
            
            // Verificar si la unidad está en movimiento
            // Consideramos ambos criterios: velocidad y cambio de posición
            bool isMoving = speedMagnitude > movementThreshold || positionDifference > movementThreshold;
            
            // Ajustar velocidad si es muy pequeña pero hay movimiento detectado
            if (isMoving && speedMagnitude < movementThreshold)
            {
                // Asignar una velocidad mínima para asegurar que se activen las animaciones
                speedMagnitude = movementThreshold * 10f;
            }
            
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
            float3 movementDirection = isMoving ? math.normalize(movementVector) : animComponent.ValueRO.MovementDirection;
            
            // Determinar si está corriendo (más del 60% de su velocidad máxima)
            bool isRunning = speedMagnitude > (maxSpeed * 0.6f);
            
            // Actualizar todos los valores del componente
            animComponent.ValueRW.CurrentSpeed = speedMagnitude;
            animComponent.ValueRW.MaxSpeed = maxSpeed;
            animComponent.ValueRW.MovementDirection = movementDirection;
            animComponent.ValueRW.IsMoving = isMoving;
            animComponent.ValueRW.IsRunning = isRunning;
            
            // LOG: Diagnóstico tras el spawn y en los primeros frames
            if (SystemAPI.Time.ElapsedTime < 2.0f || entity.Index % 100 == 0) // Solo los primeros 2s o cada 100 entidades
            {
                Debug.Log($"[UnitAnimationSystem] Entity: {entity} | CurrentPos: {currentPos} | PrevPos: {prevPos} | MovementVec: {movementVector} | Speed: {speedMagnitude:F4} | IsMoving: {isMoving}");
            }

            // Actualizar PreviousPosition para el siguiente frame
            animComponent.ValueRW.PreviousPosition = currentPos;
        }
    }
}
