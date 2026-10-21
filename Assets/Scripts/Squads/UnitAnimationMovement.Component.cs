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

        // Accepted navigation speed, including hurry/charge. MaxSpeed remains the base stat.
        public float RequestedSpeed;

        // Snapshot taken by NavMeshPositionSyncSystem after physical NavMesh movement.
        public float3 NavMeshVelocity;
        public bool HasNavMeshAgentSnapshot;
        public bool HasValidNavMeshSnapshot;
        
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
        
        // Posición previa de la unidad (para cálculo de movimiento frame a frame)
        public float3 PreviousPosition;

        // Postura actual (Normal, BracedShields, ShieldFront, ShieldSide, ShieldUp). Escrita por UnitPostureSystem.
        public UnitStance CurrentStance;

        // Fila en la formación (UnitGridSlotComponent.row). 0 = fila delantera.
        // Usada por el Animator para seleccionar variante de animación (ej. escudo al frente vs arriba).
        public int SlotRow;

        // Papel de la ranura (FormationSlotRole: 0 interior, 1 frente, 2 costado, 3 retaguardia).
        public int SlotRole;
    }
}
