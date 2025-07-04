using Unity.Entities;
using UnityEngine;

namespace ConquestTactics.Animation
{
    /// <summary>
    /// Clase de autorización para UnitAnimationMovementComponent.
    /// Se agrega a los GameObjects para generar el componente ECS correspondiente.
    /// </summary>
    public class UnitAnimationMovementAuthoring : MonoBehaviour
    {
        [Header("Initial Values")]
        [Tooltip("Velocidad inicial de la unidad")]
        public float initialSpeed = 0f;
        
        [Tooltip("Velocidad máxima de la unidad")]
        public float maxSpeed = 5f;
        
        [Tooltip("Dirección inicial de movimiento")]
        public Vector3 initialDirection = Vector3.forward;
        
        /// <summary>
        /// Baker para convertir de MonoBehaviour a componente ECS
        /// </summary>
        public class Baker : Baker<UnitAnimationMovementAuthoring>
        {
            public override void Bake(UnitAnimationMovementAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new UnitAnimationMovementComponent
                {
                    CurrentSpeed = authoring.initialSpeed,
                    MaxSpeed = authoring.maxSpeed,
                    MovementDirection = new Unity.Mathematics.float3(
                        authoring.initialDirection.x,
                        authoring.initialDirection.y,
                        authoring.initialDirection.z
                    ),
                    IsMoving = false,
                    IsRunning = false,
                    MovementTime = 0f,
                    StoppedTime = 0f
                });
            }
        }
    }
}
