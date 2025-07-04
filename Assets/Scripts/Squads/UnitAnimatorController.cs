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
            
            // Luego iniciar la secuencia completa de inicialización
            StartCoroutine(ForceAnimatorInitialization());
            
            // Log del nombre exacto del AnimatorController para verificación
            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimatorController] Animator inicializado en {gameObject.name}");
                Debug.Log($"[UnitAnimatorController] Controller: {_animator.runtimeAnimatorController.name}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro MoveSpeed: {AnimatorHasParameter(_moveSpeedHash)}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro IsStopped: {AnimatorHasParameter(_isStoppedHash)}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro CurrentGait: {AnimatorHasParameter(_currentGaitHash)}");
            }
        }
        
        private System.Collections.IEnumerator ForceAnimatorInitialization()
        {
            yield return new WaitForEndOfFrame();
            
            // Hacer un reset completo del Animator para asegurar un estado limpio
            _animator.Rebind();
            _animator.Update(0);
            
            // Configurar parámetros base
            _animator.SetBool(_isGroundedHash, true);
            _animator.SetFloat(_moveSpeedHash, 0f);
            _animator.SetInteger(_currentGaitHash, 0);
            _animator.SetBool(_isStoppedHash, true);
            _animator.SetBool(_isWalkingHash, false);
            _animator.SetBool(_movementInputPressedHash, false);
            
            // Forzar transición al estado grounded
            _animator.SetTrigger(_forceGroundedTransitionHash);
            _animator.SetTrigger(_forceIdleHash);
            
            yield return new WaitForSeconds(0.2f);
            
            // Simular una secuencia completa de movimiento para "despertar" al animator
            Debug.Log($"[UnitAnimatorController] Iniciando secuencia de despertar para {gameObject.name}");
            
            // Activar movimiento
            _animator.SetBool(_movementInputPressedHash, true);
            _animator.SetFloat(_moveSpeedHash, 0.5f);
            _animator.SetInteger(_currentGaitHash, 1);  // Walk
            _animator.SetBool(_isStoppedHash, false);
            _animator.SetBool(_isWalkingHash, true);
            _animator.SetTrigger(_forceLocomotionHash);
            
            yield return new WaitForSeconds(0.3f);
            
            // Cambiar a correr
            _animator.SetFloat(_moveSpeedHash, 0.8f);
            _animator.SetInteger(_currentGaitHash, 2);  // Run
            _animator.SetBool(_isWalkingHash, false);
            
            yield return new WaitForSeconds(0.3f);
            
            // Volver a caminar
            _animator.SetFloat(_moveSpeedHash, 0.3f);
            _animator.SetInteger(_currentGaitHash, 1);  // Walk
            _animator.SetBool(_isWalkingHash, true);
            
            yield return new WaitForSeconds(0.3f);
            
            // Volver al estado idle
            _animator.SetBool(_movementInputPressedHash, false);
            _animator.SetFloat(_moveSpeedHash, 0f);
            _animator.SetInteger(_currentGaitHash, 0);  // Idle
            _animator.SetBool(_isStoppedHash, true);
            _animator.SetBool(_isWalkingHash, false);
            _animator.SetTrigger(_forceIdleHash);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimatorController] Inicialización forzada completada para {gameObject.name}");
                Debug.Log($"[UnitAnimatorController] Estado actual: {GetCurrentAnimatorStateName()}");
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
            
            // Detectar cambios de estado para forzar transiciones
            bool isMovingThisFrame = !_isStopped;
            bool stateChanged = isMovingThisFrame != _wasMovingLastFrame;
            
            // Forzar una transición al estado grounded si está en fall state
            _animator.SetBool(_isGroundedHash, true);
            _animator.SetTrigger(_forceGroundedTransitionHash);
            
            // Actualizar velocidad de movimiento (principal para locomoción)
            // No aplicamos smoothing para que responda inmediatamente
            _animator.SetFloat(_moveSpeedHash, _currentSpeed);
            
            // Actualizar estado de detenido - IMPORTANTE para transiciones
            _animator.SetBool(_isStoppedHash, _isStopped);
            
            // Actualizar gait (idle, walk, run) - aplicamos inmediatamente
            int gait = 0; // Idle
            if (_currentSpeed > 0.01f)
            {
                gait = _isRunning ? 2 : 1; // 2 = Run, 1 = Walk
                
                // Activar el parámetro MovementInputPressed para forzar transición
                _animator.SetBool(_movementInputPressedHash, true);
                
                // Si acaba de empezar a moverse, forzar transición a locomoción
                if (stateChanged && isMovingThisFrame)
                {
                    _animator.SetTrigger(_forceLocomotionHash);
                    
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimatorController] Forzando transición a LOCOMOCIÓN para {gameObject.name}");
                    }
                }
            }
            else
            {
                _animator.SetBool(_movementInputPressedHash, false);
                
                // Si acaba de detenerse, forzar transición a idle
                if (stateChanged && !isMovingThisFrame)
                {
                    _animator.SetTrigger(_forceIdleHash);
                    
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimatorController] Forzando transición a IDLE para {gameObject.name}");
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
                          $"Running: {_isRunning}, Gait: {gait}, AnimState: {GetCurrentAnimatorStateName()}");
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
    }
    
    /// <summary>
    /// Atributo para mostrar campos de solo lectura en el inspector
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}
