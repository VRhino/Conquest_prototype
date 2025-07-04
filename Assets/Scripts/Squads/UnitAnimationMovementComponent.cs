using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ConquestTactics.Animation
{
    /// <summary>
    /// Componente ECS para almacenar datos de movimiento necesarios para animar unidades.
    /// Se añade a las entidades de unidades y contiene información sobre su velocidad, dirección, etc.
    /// </summary>
    public struct UnitAnimationMovementComponent : IComponentData
    {
        // Velocidad actual de la unidad
        public float CurrentSpeed;
        
        // Velocidad máxima de la unidad (para normalizar)
        public float MaxSpeed;
        
        // Dirección normalizada de movimiento
        public float3 MovementDirection;
        
        // Si la unidad está actualmente en movimiento
        public bool IsMoving;
        
        // Si la unidad está corriendo (velocidad > 50% máxima)
        public bool IsRunning;
        
        // Tiempo que lleva la unidad en movimiento (para transiciones)
        public float MovementTime;
        
        // Tiempo que lleva la unidad detenida (para transiciones)
        public float StoppedTime;
    }
}
