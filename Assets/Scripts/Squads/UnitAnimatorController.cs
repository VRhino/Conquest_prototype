using UnityEngine;
using ConquestTactics.Animation;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Controlador para las animaciones de unidades en escuadrón.
    /// Conecta el UnitAnimationAdapter con el Animator usando los parámetros
    /// del AC_Polygon_Masculine.controller.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(UnitAnimationAdapter))]
    public class UnitAnimatorController : MonoBehaviour
    {
        #region Inspector Settings
        
        [Header("Animation Settings")]
        [Tooltip("Velocidad de transición entre animaciones")]
        [SerializeField] private float _animationDampTime = 0.1f;
        
        [Tooltip("Velocidad de rotación para mirar hacia la dirección del movimiento")]
        [SerializeField] private float _rotationSpeed = 10f;
        
        [Tooltip("Si está habilitado, la unidad rotará hacia la dirección del movimiento")]
        [SerializeField] private bool _rotateToMovementDirection = true;
        
        [Header("Debug")]
        [Tooltip("Mostrar información de debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        [Header("Runtime Info (ReadOnly)")]
        [SerializeField, ReadOnly] private float _currentSpeed;
        [SerializeField, ReadOnly] private bool _isStopped;
        [SerializeField, ReadOnly] private bool _isRunning;
        [SerializeField, ReadOnly] private bool _isSprinting;
        
        #endregion
        
        #region Private Fields
        
        // Referencias a componentes
        private Animator _animator;
        private UnitAnimationAdapter _animationAdapter;
        
        // Hashes de parámetros del animator (AC_Polygon_Masculine.controller)
        private readonly int _moveSpeedHash = Animator.StringToHash("MoveSpeed");
        private readonly int _isStoppedHash = Animator.StringToHash("IsStopped");
        private readonly int _currentGaitHash = Animator.StringToHash("CurrentGait");
        private readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
        private readonly int _isWalkingHash = Animator.StringToHash("IsWalking");
        
        // Estados adicionales para compatibilidad con AC_Polygon
        private readonly int _strafeDirectionXHash = Animator.StringToHash("StrafeDirectionX");
        private readonly int _strafeDirectionZHash = Animator.StringToHash("StrafeDirectionZ");
        
        // Parámetros adicionales que pueden ser necesarios para el controller AC_Polygon_Masculine
        private readonly int _movementInputPressedHash = Animator.StringToHash("MovementInputPressed");
        private readonly int _forceGroundedTransitionHash = Animator.StringToHash("ForceGroundedTransition");
        
        // Triggers para forzar transiciones específicas
        private readonly int _forceLocomotionHash = Animator.StringToHash("ForceLocomotion");
        private readonly int _forceIdleHash = Animator.StringToHash("ForceIdle");
        
        // Variable para detectar cambios de estado
        private bool _wasMovingLastFrame = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animationAdapter = GetComponent<UnitAnimationAdapter>();
            
            if (_animator == null || _animationAdapter == null)
            {
                Debug.LogError($"[UnitAnimatorController] Configuración incompleta en {gameObject.name}. Asegúrate de que tiene Animator y UnitAnimationAdapter");
                enabled = false;
                return;
            }
            
            // Inicializar los valores del Animator
            _animator.SetBool(_isGroundedHash, true); // Siempre grounded para evitar problemas de Fall state
            _animator.SetBool(_isStoppedHash, true); // Iniciar detenido
            _animator.SetFloat(_moveSpeedHash, 0f); // Velocidad inicial 0
            _animator.SetInteger(_currentGaitHash, 0); // Gait inicial = Idle
            _animator.SetBool(_isWalkingHash, false); // No está caminando inicialmente
            
            // Resetear direcciones de strafe
            _animator.SetFloat(_strafeDirectionXHash, 0f);
            _animator.SetFloat(_strafeDirectionZHash, 1f); // Forward direction
            
            // Forzar una transición al estado grounded
            _animator.SetTrigger(_forceGroundedTransitionHash);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimatorController] Inicializado en {gameObject.name}");
            }
        }
        
        private void Start()
        {
            // Verificar que la referencia del Animator está presente
            if (_animator == null)
            {
                Debug.LogError($"[UnitAnimatorController] No se encontró Animator en {gameObject.name}");
                return;
            }
            
            // Verificar que hay un controller asignado
            if (_animator.runtimeAnimatorController == null)
            {
                Debug.LogError($"[UnitAnimatorController] No hay AnimatorController asignado en {gameObject.name}");
                return;
            }
            
            // Activar los triggers directamente al inicio
            _animator.SetBool(_isGroundedHash, true);
            _animator.SetBool(_isStoppedHash, true);
            _animator.SetFloat(_moveSpeedHash, 0f);
            _animator.SetInteger(_currentGaitHash, 0);
            _animator.SetBool(_isWalkingHash, false);
            _animator.SetTrigger(_forceIdleHash);
            
            // Log del nombre exacto del AnimatorController para verificación
            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimatorController] Animator inicializado en {gameObject.name}");
                Debug.Log($"[UnitAnimatorController] Controller: {_animator.runtimeAnimatorController.name}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro MoveSpeed: {AnimatorHasParameter(_moveSpeedHash)}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro IsStopped: {AnimatorHasParameter(_isStoppedHash)}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro CurrentGait: {AnimatorHasParameter(_currentGaitHash)}");
                // Log de todos los parámetros actuales del Animator
                string paramStates = $"[UnitAnimatorController] Estado inicial de parámetros para {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}):";
                foreach (var param in _animator.parameters)
                {
                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Bool:
                            paramStates += $"\n  {param.name} (Bool): {_animator.GetBool(param.name)}";
                            break;
                        case AnimatorControllerParameterType.Float:
                            paramStates += $"\n  {param.name} (Float): {_animator.GetFloat(param.name):F2}";
                            break;
                        case AnimatorControllerParameterType.Int:
                            paramStates += $"\n  {param.name} (Int): {_animator.GetInteger(param.name)}";
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            paramStates += $"\n  {param.name} (Trigger)";
                            break;
                    }
                }
                Debug.Log(paramStates);
                Debug.Log($"[UnitAnimatorController] Estado inicial del Animator: {GetCurrentAnimatorStateName()}");
            }
        }
        
        private void Update()
        {
            UpdateAnimationParameters();
            
            if (_rotateToMovementDirection)
            {
                RotateToMovementDirection();
            }
        }
        
        #endregion
        
        #region Animation Update
        
        /// <summary>
        /// Actualiza los parámetros del Animator basados en los datos del adapter
        /// </summary>
        private void UpdateAnimationParameters()
        {
            // Almacenar valores actuales para debugging
            _currentSpeed = _animationAdapter.NormalizedSpeed;
            _isStopped = _animationAdapter.IsStopped;
            _isRunning = _animationAdapter.IsRunning;
            _isSprinting = _animationAdapter.IsSprinting;

            // Log de valores recibidos del adapter para comparar
            if (_enableDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[UnitAnimatorController][Adapter] {gameObject.name} - Adapter: Speed={_animationAdapter.NormalizedSpeed:F2}, Stopped={_animationAdapter.IsStopped}, Running={_animationAdapter.IsRunning}, Vec={_animationAdapter.MovementVector}");
            }

            // Detectar cambios de estado para forzar transiciones
            bool isMovingThisFrame = !_isStopped;
            bool stateChanged = isMovingThisFrame != _wasMovingLastFrame;
            
            // IMPORTANTE: Verificar el estado actual del Animator
            AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
            bool isInIdleState = currentState.IsName("Base Layer.Idle Standing.Idle_Standing") || 
                                currentState.IsName("Idle_Standing");
            bool isInLocoState = currentState.IsName("Base Layer.Locomotion Standing.Locomotion") || 
                                currentState.IsName("Locomotion");
            
            // Forzar una transición al estado grounded si está en fall state
            _animator.SetBool(_isGroundedHash, true);
            
            if (currentState.IsName("Base Layer.Fall.Falling") || currentState.IsName("Falling"))
            {
                _animator.SetTrigger(_forceGroundedTransitionHash);
            }
            
            // MODIFICADO: Forzar valores para las animaciones
            // Primero actualizamos parámetros principales en el orden correcto
            
            // 0. Forzar parámetros adicionales requeridos por el Animator Controller
            _animator.SetBool("isStarting", false);
            _animator.SetBool("isJumping", false);
            _animator.SetBool("MovementInputTapper", false);
            _animator.SetBool("MovementInputHeld", isMovingThisFrame);
            
            // 1. Activar/desactivar movementInput (crítico para transiciones)
            _animator.SetBool(_movementInputPressedHash, isMovingThisFrame);
            
            // 2. Establecer velocidad (crítico para determinar qué animación se muestra)
            _animator.SetFloat(_moveSpeedHash, _currentSpeed);
            
            // 3. Forzar el estado IsStopped a false cuando hay movimiento (prioridad máxima)
            if (isMovingThisFrame)
            {
                _animator.SetBool(_isStoppedHash, false);
            }
            else
            {
                _animator.SetBool(_isStoppedHash, true);
            }
            
            // 4. Establecer gait explícitamente
            int gait;
            if (isMovingThisFrame)
            {
                if (_isSprinting)
                    gait = 3; // Sprint
                else if (_isRunning)
                    gait = 2; // Run
                else
                    gait = 1; // Walk
            }
            else
            {
                gait = 0; // Idle
            }
            _animator.SetInteger(_currentGaitHash, gait);
            
            // 5. Establecer IsWalking para mayor claridad
            _animator.SetBool(_isWalkingHash, isMovingThisFrame && !_isRunning);
            
            // 6. Forzar transiciones con triggers si el estado no coincide con el esperado
            // Esto soluciona problemas donde el animator no responde a los parámetros regulares
            
            if (isMovingThisFrame && isInIdleState) 
            {
                _animator.SetTrigger(_forceLocomotionHash);
                if (_enableDebugLogs)
                {
                    Debug.LogWarning($"[UnitAnimatorController] CORRECCIÓN: Forzando salida de IDLE para {gameObject.name} - Speed: {_currentSpeed:F2}");
                }
            }
            else if (!isMovingThisFrame && isInLocoState && currentState.normalizedTime > 0.25f)
            {
                _animator.SetTrigger(_forceIdleHash);
                if (_enableDebugLogs)
                {
                    Debug.LogWarning($"[UnitAnimatorController] CORRECCIÓN: Forzando salida de LOCOMOTION para {gameObject.name}");
                }
            }
            
            // Si cambió de estado, aplicar triggers correspondientes
            if (stateChanged)
            {
                if (isMovingThisFrame)
                {
                    _animator.SetTrigger(_forceLocomotionHash);
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimatorController] Cambio: IDLE → LOCOMOCIÓN para {gameObject.name}");
                    }
                }
                else
                {
                    _animator.SetTrigger(_forceIdleHash);
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimatorController] Cambio: LOCOMOCIÓN → IDLE para {gameObject.name}");
                    }
                }
            }
            
            _animator.SetInteger(_currentGaitHash, gait);
            
            // Configurar caminando o corriendo - más explícito
            bool isWalking = !_isRunning && !_isStopped;
            _animator.SetBool(_isWalkingHash, isWalking);
            
            // Parámetros de dirección para compatibilidad con AC_Polygon
            if (!_isStopped)
            {
                // No aplicamos smoothing para direcciones para que sean más responsivas
                _animator.SetFloat(_strafeDirectionXHash, _animationAdapter.MovementVector.x);
                _animator.SetFloat(_strafeDirectionZHash, _animationAdapter.MovementVector.y);
            }
            else
            {
                // En estado detenido, resetear strafe directions
                _animator.SetFloat(_strafeDirectionXHash, 0f);
                _animator.SetFloat(_strafeDirectionZHash, 1f); // Forward direction cuando está detenido
            }
            
            // Guardar estado para detectar cambios en el próximo frame
            _wasMovingLastFrame = isMovingThisFrame;
            
            // Registrar información de debugging en el log
            if (_enableDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[UnitAnimatorController] {gameObject.name} - Speed: {_currentSpeed:F2}, Stopped: {_isStopped}, " +
                          $"Running: {_isRunning}, Sprinting: {_isSprinting}, Gait: {gait}, AnimState: {GetCurrentAnimatorStateName()}");
            }
        }
        
        /// <summary>
        /// Rota la unidad para que mire hacia la dirección del movimiento
        /// </summary>
        private void RotateToMovementDirection()
        {
            if (_isStopped || _animationAdapter.MovementVector.sqrMagnitude < 0.01f)
                return;
                
            Vector3 direction = new Vector3(
                _animationAdapter.MovementVector.x,
                0f,
                _animationAdapter.MovementVector.y
            ).normalized;
            
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * Time.deltaTime
                );
            }
        }
        
        /// <summary>
        /// Obtiene el nombre del estado actual del animator para depuración
        /// </summary>
        private string GetCurrentAnimatorStateName()
        {
            if (_animator == null) return "Animator is null";
            
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            
            // Intentar determinar el estado actual por hash o nombre
            if (stateInfo.IsName("Base Layer.Locomotion Standing.Locomotion"))
                return "Locomotion";
            else if (stateInfo.IsName("Base Layer.Idle Standing.Idle_Standing"))
                return "Idle_Standing";
            else if (stateInfo.IsName("Base Layer.Fall.Falling"))
                return "Falling";
            else if (stateInfo.shortNameHash == Animator.StringToHash("Locomotion"))
                return "Locomotion";
            else if (stateInfo.shortNameHash == Animator.StringToHash("Idle_Standing"))
                return "Idle_Standing";
            
            // Si no podemos identificar el estado, mostrar información del hash
            return $"Unknown State ({stateInfo.shortNameHash})";
        }
        
        /// <summary>
        /// Verifica si un parámetro existe en el Animator
        /// </summary>
        private bool AnimatorHasParameter(int parameterHash)
        {
            if (_animator == null) return false;
            
            foreach (AnimatorControllerParameter param in _animator.parameters)
            {
                if (param.nameHash == parameterHash)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Fuerza la animación de movimiento - llamar cuando una unidad se atasca en idle
        /// </summary>
        public void ForceMovementAnimation()
        {
            _animator.SetBool(_isGroundedHash, true);
            _animator.SetBool(_isStoppedHash, false);
            _animator.SetFloat(_moveSpeedHash, 0.5f); // Velocidad media
            _animator.SetInteger(_currentGaitHash, 1); // Walk
            _animator.SetBool(_movementInputPressedHash, true);
            _animator.SetBool(_isWalkingHash, true);
            _animator.SetTrigger(_forceLocomotionHash);
            if (_enableDebugLogs)
            {
                Debug.LogWarning($"[UnitAnimatorController] FORZANDO animación de movimiento para {gameObject.name}");
            }
        }
        
        /// <summary>
        /// Fuerza la animación de idle - llamar cuando una unidad se atasca en movimiento
        /// </summary>
        public void ForceIdleAnimation()
        {
            _animator.SetBool(_isGroundedHash, true);
            _animator.SetBool(_isStoppedHash, true);
            _animator.SetFloat(_moveSpeedHash, 0f);
            _animator.SetInteger(_currentGaitHash, 0); // Idle
            _animator.SetBool(_movementInputPressedHash, false);
            _animator.SetBool(_isWalkingHash, false);
            _animator.SetTrigger(_forceIdleHash);
            if (_enableDebugLogs)
            {
                Debug.LogWarning($"[UnitAnimatorController] FORZANDO animación de idle para {gameObject.name}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Atributo para mostrar campos de solo lectura en el inspector
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}
