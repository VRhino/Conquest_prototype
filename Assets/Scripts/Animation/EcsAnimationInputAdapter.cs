using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;

namespace ConquestTactics.Animation
{
    /// <summary>
    /// Adaptador que conecta el sistema ECS de input con el SamplePlayerAnimationController de Synty.
    /// Simula la interfaz del InputReader original pero obtiene los datos del sistema ECS.
    /// </summary>
    public class EcsAnimationInputAdapter : MonoBehaviour
    {
        #region Public Properties (Simulan InputReader)
        
        /// <summary>
        /// Vector de movimiento compuesto (X = horizontal, Y = forward/backward)
        /// </summary>
        public Vector2 _moveComposite { get; private set; }
        
        /// <summary>
        /// Duración del input de movimiento (para detectar tapped/pressed/held)
        /// </summary>
        public float _movementInputDuration { get; set; }
        
        /// <summary>
        /// True si se detecta input de movimiento
        /// </summary>
        public bool _movementInputDetected { get; private set; }
        
        /// <summary>
        /// Delta del mouse para control de cámara (mantenido para compatibilidad)
        /// </summary>
        public Vector2 _mouseDelta { get; private set; }
        
        #endregion
        
        #region Events (Solo los necesarios para el juego)
        
        /// <summary>
        /// Evento cuando se togglea el modo walk
        /// </summary>
        public Action onWalkToggled;
        
        /// <summary>
        /// Evento cuando se activa el sprint
        /// </summary>
        public Action onSprintActivated;
        
        /// <summary>
        /// Evento cuando se desactiva el sprint
        /// </summary>
        public Action onSprintDeactivated;
        
        #endregion
        
        #region Inspector Settings
        
        [Header("ECS Configuration")]
        [Tooltip("Si está marcado, busca automáticamente la entidad héroe")]
        [SerializeField] private bool _autoFindHeroEntity = true;
        
        [Tooltip("Threshold para detectar input como movimiento válido")]
        [SerializeField] private float _inputThreshold = 0.01f;
        
        [Header("Debug")]
        [Tooltip("Mostrar información de debug en consola")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Private Fields
        
        private Entity _heroEntity;
        private EntityManager _entityManager;
        private World _world;
        
        // Estados previos para detectar cambios
        private bool _previousSprintPressed;
        private bool _previousWalkTogglePressed;
        private float _inputStartTime;
        
        // Cache para evitar allocaciones
        private EntityQuery _heroQuery;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            InitializeEcsReferences();
            
            if (_autoFindHeroEntity)
            {
                FindHeroEntity();
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log("[EcsAnimationInputAdapter] Initialized successfully");
            }
        }
        
        private void Update()
        {
            if (!IsValidSetup())
                return;
                
            UpdateInputFromEcs();
            ProcessInputEvents();
            UpdateMovementTiming();
        }
        
        private void OnDestroy()
        {
            // EntityQuery is a struct and doesn't need explicit disposal
        }
        
        #endregion
        
        #region ECS Initialization
        
        private void InitializeEcsReferences()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogError("[EcsAnimationInputAdapter] No se encontró World ECS por defecto");
                return;
            }
            
            _entityManager = _world.EntityManager;
            
            // Crear query para encontrar entidades héroe
            _heroQuery = _entityManager.CreateEntityQuery(typeof(HeroInputComponent));
        }
        
        private void FindHeroEntity()
        {
            var heroEntities = _heroQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (heroEntities.Length > 0)
            {
                _heroEntity = heroEntities[0];
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[EcsAnimationInputAdapter] Entidad héroe encontrada: {_heroEntity}");
                }
            }
            else
            {
                if (_enableDebugLogs)
                {
                    Debug.LogWarning("[EcsAnimationInputAdapter] No se encontró entidad con HeroInputComponent");
                }
            }
            
            heroEntities.Dispose();
        }
        
        private bool IsValidSetup()
        {
            return _world != null && 
                   _entityManager != null && 
                   _heroEntity != Entity.Null && 
                   _entityManager.Exists(_heroEntity);
        }
        
        #endregion
        
        #region Input Processing
        
        /// <summary>
        /// Obtiene los datos de input del sistema ECS y los convierte al formato esperado
        /// </summary>
        private void UpdateInputFromEcs()
        {
            try
            {
                // Obtener el componente de input del héroe
                var heroInput = _entityManager.GetComponentData<HeroInputComponent>(_heroEntity);
                
                // Convertir float2 a Vector2 para compatibilidad
                _moveComposite = new Vector2(heroInput.MoveInput.x, heroInput.MoveInput.y);
                
                // Detectar si hay input de movimiento
                _movementInputDetected = math.lengthsq(heroInput.MoveInput) > (_inputThreshold * _inputThreshold);
                
                // TODO: Agregar mouse delta si se necesita en el futuro
                // _mouseDelta = new Vector2(heroInput.MouseDelta.x, heroInput.MouseDelta.y);
                
                if (_enableDebugLogs && _movementInputDetected)
                {
                    Debug.Log($"[EcsAnimationInputAdapter] Movement Input: {_moveComposite}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EcsAnimationInputAdapter] Error al obtener input de ECS: {e.Message}");
                
                // Resetear valores en caso de error
                _moveComposite = Vector2.zero;
                _movementInputDetected = false;
            }
        }
        
        /// <summary>
        /// Procesa eventos de input para sprint y walk toggle
        /// </summary>
        private void ProcessInputEvents()
        {
            try
            {
                var heroInput = _entityManager.GetComponentData<HeroInputComponent>(_heroEntity);
                
                // Obtener estados actuales de botones
                bool currentSprintPressed = heroInput.IsSprintPressed;
                bool currentWalkTogglePressed = heroInput.IsWalkTogglePressed;
                
                // Procesar eventos de sprint
                if (currentSprintPressed && !_previousSprintPressed)
                {
                    onSprintActivated?.Invoke();
                    if (_enableDebugLogs)
                    {
                        Debug.Log("[EcsAnimationInputAdapter] Sprint Activated");
                    }
                }
                else if (!currentSprintPressed && _previousSprintPressed)
                {
                    onSprintDeactivated?.Invoke();
                    if (_enableDebugLogs)
                    {
                        Debug.Log("[EcsAnimationInputAdapter] Sprint Deactivated");
                    }
                }
                
                // Procesar walk toggle
                if (currentWalkTogglePressed && !_previousWalkTogglePressed)
                {
                    onWalkToggled?.Invoke();
                    if (_enableDebugLogs)
                    {
                        Debug.Log("[EcsAnimationInputAdapter] Walk Toggled");
                    }
                }
                
                // Actualizar estados previos
                _previousSprintPressed = currentSprintPressed;
                _previousWalkTogglePressed = currentWalkTogglePressed;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EcsAnimationInputAdapter] Error al procesar eventos: {e.Message}");
            }
        }
        
        /// <summary>
        /// Actualiza el timing del input para detectar tapped/pressed/held
        /// </summary>
        private void UpdateMovementTiming()
        {
            if (_movementInputDetected)
            {
                if (_movementInputDuration == 0)
                {
                    _inputStartTime = Time.time;
                }
                _movementInputDuration = Time.time - _inputStartTime;
            }
            else
            {
                _movementInputDuration = 0;
                _inputStartTime = 0;
            }
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Permite configurar manualmente una entidad héroe específica
        /// </summary>
        /// <param name="heroEntity">La entidad héroe a usar</param>
        public void SetHeroEntity(Entity heroEntity)
        {
            _heroEntity = heroEntity;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EcsAnimationInputAdapter] Entidad héroe configurada manualmente: {heroEntity}");
            }
        }
        
        /// <summary>
        /// Fuerza la búsqueda de una nueva entidad héroe
        /// </summary>
        public void RefreshHeroEntity()
        {
            FindHeroEntity();
        }
        
        /// <summary>
        /// Simula el evento de walk toggle (para testing o uso manual)
        /// </summary>
        public void TriggerWalkToggle()
        {
            onWalkToggled?.Invoke();
        }
        
        #endregion
        
        #region Debug & Utilities
        
        /// <summary>
        /// Para debugging: muestra el estado actual del input
        /// </summary>
        [ContextMenu("Debug Input State")]
        public void DebugInputState()
        {
            Debug.Log($"=== ECS Animation Input Adapter State ===");
            Debug.Log($"Hero Entity: {_heroEntity}");
            Debug.Log($"Move Composite: {_moveComposite}");
            Debug.Log($"Movement Detected: {_movementInputDetected}");
            Debug.Log($"Movement Duration: {_movementInputDuration}");
            Debug.Log($"Sprint Pressed: {_previousSprintPressed}");
            Debug.Log($"Valid Setup: {IsValidSetup()}");
        }
        
        /// <summary>
        /// Información del estado para el inspector
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying && _enableDebugLogs)
            {
                // Solo ejecutar en play mode para evitar errores en edit mode
            }
        }
        
        #endregion
    }
}
