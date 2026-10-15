using UnityEngine;
using System.Collections.Generic;
using ConquestTactics.Animation;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Controlador para las animaciones de unidades en escuadrón.
    /// Conecta el UnitAnimationAdapter con el Animator usando los parámetros
    /// del AC_Polygon_Masculine.controller.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(UnitAnimationAdapter))]
    public class UnitAnimatorController : MonoBehaviour
    {
        #region Inspector Settings
        
        [Header("Animation Settings")]
        [Tooltip("Velocidad de transición entre animaciones")]
        [SerializeField] private float _animationDampTime = 0.1f;
        [SerializeField, Min(0f)] private float _debugMovementBlendSpeed = 3f;
        
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
        private RuntimeAnimatorController _parameterController;
        private bool _parametersCached;
        private readonly HashSet<int> _parameterHashes = new();
        private UnitAnimationAdapter _animationAdapter;
        
        // Animation parameter hashes are centralized in AnimationHashes.cs
        
        // Variable para detectar cambios de estado
        private bool _wasMovingLastFrame = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.applyRootMotion = false;
            _animationAdapter = GetComponent<UnitAnimationAdapter>();
            
            if (_animator == null || _animationAdapter == null)
            {
                Debug.LogError($"[UnitAnimatorController] Configuración incompleta en {gameObject.name}. Asegúrate de que tiene Animator y UnitAnimationAdapter");
                enabled = false;
                return;
            }
            
            // Inicializar los valores del Animator
            _animator.SetBool(AnimationHashes.IsGrounded, true); // Siempre grounded para evitar problemas de Fall state
            _animator.SetBool(AnimationHashes.IsStopped, true); // Iniciar detenido
            _animator.SetFloat(AnimationHashes.MoveSpeed, 0f); // Velocidad inicial 0
            _animator.SetInteger(AnimationHashes.CurrentGait, 0); // Gait inicial = Idle
            _animator.SetBool(AnimationHashes.IsWalking, false); // No está caminando inicialmente

            // Resetear direcciones de strafe
            _animator.SetFloat(AnimationHashes.StrafeDirectionX, 0f);
            _animator.SetFloat(AnimationHashes.StrafeDirectionZ, 1f); // Forward direction
            
            // Forzar una transición al estado grounded
            //_animator.SetTrigger(_forceGroundedTransitionHash);
            
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
            _animator.SetBool(AnimationHashes.IsGrounded, true);
            _animator.SetBool(AnimationHashes.IsStopped, true);
            _animator.SetFloat(AnimationHashes.MoveSpeed, 0f);
            _animator.SetInteger(AnimationHashes.CurrentGait, 0);
            _animator.SetBool(AnimationHashes.IsWalking, false);
            //_animator.SetTrigger(_forceIdleHash);
            
            // Log del nombre exacto del AnimatorController para verificación
            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimatorController] Animator inicializado en {gameObject.name}");
                Debug.Log($"[UnitAnimatorController] Controller: {_animator.runtimeAnimatorController.name}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro MoveSpeed: {AnimatorHasParameter(AnimationHashes.MoveSpeed)}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro IsStopped: {AnimatorHasParameter(AnimationHashes.IsStopped)}");
                Debug.Log($"[UnitAnimatorController] Tiene parámetro CurrentGait: {AnimatorHasParameter(AnimationHashes.CurrentGait)}");
                // Log de todos los parámetros actuales del Animator
                string paramStates = $"[UnitAnimatorController] Estado inicial de parámetros para {gameObject.name} (EntityId: {gameObject.GetEntityId()}):";
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
        }
        
        #endregion
        
        #region Animation Update
        
        /// <summary>
        /// Actualiza los parámetros del Animator basados en los datos del adapter
        /// </summary>
        private void UpdateAnimationParameters()
        {
            _currentSpeed = _animationAdapter.Speed2D;
            _isStopped = _animationAdapter.IsStopped;
            _isRunning = _animationAdapter.IsRunning;
            _isSprinting = _animationAdapter.IsSprinting;
            bool moving = !_isStopped;
            bool started = moving && !_wasMovingLastFrame;
            var gait = _animationAdapter.Gait;
            Vector2 direction = moving ? _animationAdapter.MovementVector : Vector2.up;

            _animator.SetBool(AnimationHashes.IsGrounded, true);
            _animator.SetBool(AnimationHashes.IsStopped, !moving);
            _animator.SetBool(AnimationHashes.MovementInputTapped, started);
            _animator.SetBool(AnimationHashes.MovementInputPressed, started);
            _animator.SetBool(AnimationHashes.MovementInputHeld, moving && !started);
            _animator.SetInteger(AnimationHashes.CurrentGait, (int)gait);
            _animator.SetBool(AnimationHashes.IsWalking, gait == LocomotionGait.Walk);
            if (moving)
                _animator.SetFloat(AnimationHashes.MoveSpeed, _animationAdapter.AnimatorSpeed,
                    _animationDampTime, Time.deltaTime);
            else
                _animator.SetFloat(AnimationHashes.MoveSpeed, 0f);
            _animator.SetFloat(AnimationHashes.IsStrafing, moving ? 1f : 0f);
            _animator.SetFloat(AnimationHashes.StrafeDirectionX, direction.x);
            _animator.SetFloat(AnimationHashes.StrafeDirectionZ, direction.y);
            _animator.SetFloat(AnimationHashes.ForwardStrafe, direction.y >= 0f ? 1f : 0f);

            if (AnimatorHasParameter(AnimationHashes.IsAttacking))
                _animator.SetBool(AnimationHashes.IsAttacking, _animationAdapter.IsAttacking);
            if (_animationAdapter.JustShot && AnimatorHasParameter(AnimationHashes.TriggerShoot))
                _animator.SetTrigger(AnimationHashes.TriggerShoot);
            if (AnimatorHasParameter(AnimationHashes.IsReloading))
                _animator.SetBool(AnimationHashes.IsReloading, _animationAdapter.IsReloadingRanged);
            _wasMovingLastFrame = moving;
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
            
            if (!_parametersCached || _parameterController != _animator.runtimeAnimatorController)
            {
                _parameterHashes.Clear();
                foreach (var parameter in _animator.parameters)
                    _parameterHashes.Add(parameter.nameHash);
                _parameterController = _animator.runtimeAnimatorController;
                _parametersCached = true;
            }
            return _parameterHashes.Contains(parameterHash);
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Fuerza la animación de movimiento - llamar cuando una unidad se atasca en idle
        /// </summary>
        public void ForceMovementAnimation()
        {
            _animator.SetBool(AnimationHashes.IsGrounded, true);
            _animator.SetBool(AnimationHashes.IsStopped, false);
            _animator.SetFloat(AnimationHashes.MoveSpeed, _debugMovementBlendSpeed);
            _animator.SetInteger(AnimationHashes.CurrentGait, (int)LocomotionGait.Walk);
            _animator.SetBool(AnimationHashes.MovementInputPressed, true);
            _animator.SetBool(AnimationHashes.IsWalking, true);
            if (AnimatorHasParameter(AnimationHashes.ForceLocomotion))
                _animator.SetTrigger(AnimationHashes.ForceLocomotion);
            if (_enableDebugLogs)
            {
                // Debug.LogWarning($"[UnitAnimatorController] FORZANDO animación de movimiento para {gameObject.name}");
            }
        }
        
        /// <summary>
        /// Fuerza la animación de idle - llamar cuando una unidad se atasca en movimiento
        /// </summary>
        public void ForceIdleAnimation()
        {
            _animator.SetBool(AnimationHashes.IsGrounded, true);
            _animator.SetBool(AnimationHashes.IsStopped, true);
            _animator.SetFloat(AnimationHashes.MoveSpeed, 0f);
            _animator.SetInteger(AnimationHashes.CurrentGait, 0); // Idle
            _animator.SetBool(AnimationHashes.MovementInputPressed, false);
            _animator.SetBool(AnimationHashes.IsWalking, false);
            _animator.SetBool(AnimationHashes.MovementInputTapped, false);
            _animator.SetBool(AnimationHashes.MovementInputHeld, false);
            if (_enableDebugLogs)
            {
                // Debug.LogWarning($"[UnitAnimatorController] FORZANDO animación de idle para {gameObject.name}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Atributo para mostrar campos de solo lectura en el inspector
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}
