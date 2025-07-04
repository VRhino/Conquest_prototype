using UnityEngine;
using ConquestTactics.Animation;
using ConquestTactics.Visual;

namespace ConquestTactics.Testing
{
    /// <summary>
    /// Script para testear las animaciones de unidades en modo debug.
    /// Permite visualizar información de animación y forzar estados.
    /// </summary>
    public class UnitAnimationTester : MonoBehaviour
    {
        [Header("Components to Test")]
        [SerializeField] private UnitAnimationAdapter _animationAdapter;
        [SerializeField] private UnitAnimatorController _animatorController;
        [SerializeField] private Animator _animator;
        
        [Header("Debug Settings")]
        [SerializeField] private bool _showDebugInfo = true;
        [SerializeField] private bool _logAnimationEvents = false;
        
        [Header("Manual Testing")]
        [SerializeField] private bool _enableManualTesting = false;
        [SerializeField] private float _manualSpeed = 0f;
        [SerializeField] private Vector2 _manualDirection = Vector2.up;
        [SerializeField] private bool _manualIsRunning = false;
        
        [Header("Force Animation")]
        [SerializeField] private bool _enableForceAnimationControls = false;
        [SerializeField] private KeyCode _forceLocomotionKey = KeyCode.F1;
        [SerializeField] private KeyCode _forceIdleKey = KeyCode.F2;
        [SerializeField] private KeyCode _resetAnimatorKey = KeyCode.F3;

        [Header("Direct Animator Controls")]
        [SerializeField] private bool _enableDirectAnimatorControls = false;
        
        [SerializeField] private KeyCode _setWalkingKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode _setRunningKey = KeyCode.Alpha2;
        [SerializeField] private KeyCode _setIdleKey = KeyCode.Alpha0;
        [SerializeField] private KeyCode _inspectAnimatorKey = KeyCode.I;

        // Variables para almacenar información de estado del Animator
        private bool _wasInLocoState = false;
        private string _currentStateName = "";
        private float _timeInCurrentState = 0f;
        
        private void Start()
        {
            // Auto-find components if not assigned
            if (_animationAdapter == null)
                _animationAdapter = GetComponentInChildren<UnitAnimationAdapter>();
                
            if (_animatorController == null)
                _animatorController = GetComponentInChildren<UnitAnimatorController>();
                
            if (_animator == null && _animatorController != null)
                _animator = _animatorController.GetComponent<Animator>();
        }
        
        private void Update()
        {
            if (_enableManualTesting)
            {
                HandleManualTesting();
            }
            
            if (_enableForceAnimationControls)
            {
                HandleForceAnimationControls();
            }
            
            if (_enableDirectAnimatorControls)
            {
                HandleDirectAnimatorControls();
            }
        }
        
        private void HandleManualTesting()
        {
            // Esta función existe solo para pruebas en el editor
            // No debería usarse en producción
            
            // Aquí podemos ajustar manualmente los valores del adapter
            // en lugar de que vengan desde ECS
            
            // Esta implementación se completará si es necesario para testing
        }
        
        private void HandleForceAnimationControls()
        {
            // Forzar transición a estado de locomoción
            if (Input.GetKeyDown(_forceLocomotionKey) && _animator != null)
            {
                _animator.SetTrigger("ForceLocomotion");
            }
            
            // Forzar transición a estado de idle
            if (Input.GetKeyDown(_forceIdleKey) && _animator != null)
            {
                _animator.SetTrigger("ForceIdle");
            }
            
            // Reiniciar animator a estado por defecto
            if (Input.GetKeyDown(_resetAnimatorKey) && _animator != null)
            {
                _animator.Rebind();
            }
        }

        private void HandleDirectAnimatorControls()
        {
            if (_animator == null) return;
            
            // Control directo de estados de animación
            if (Input.GetKeyDown(_setWalkingKey))
            {
                SetAnimatorWalking();
                Debug.Log("Forzando WALK state");
            }
            else if (Input.GetKeyDown(_setRunningKey))
            {
                SetAnimatorRunning();
                Debug.Log("Forzando RUN state");
            }
            else if (Input.GetKeyDown(_setIdleKey))
            {
                SetAnimatorIdle();
                Debug.Log("Forzando IDLE state");
            }
            
            // Inspeccionar el animator
            if (Input.GetKeyDown(_inspectAnimatorKey))
            {
                InspectAnimator();
            }
        }
        
        private void SetAnimatorWalking()
        {
            if (_animator == null) return;
            
            _animator.SetFloat("MoveSpeed", 0.5f);
            _animator.SetInteger("CurrentGait", 1);
            _animator.SetBool("IsStopped", false);
            _animator.SetBool("IsWalking", true);
            _animator.SetBool("MovementInputPressed", true);
            _animator.SetTrigger("ForceLocomotion");
        }
        
        private void SetAnimatorRunning()
        {
            if (_animator == null) return;
            
            _animator.SetFloat("MoveSpeed", 0.8f);
            _animator.SetInteger("CurrentGait", 2);
            _animator.SetBool("IsStopped", false);
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("MovementInputPressed", true);
            _animator.SetTrigger("ForceLocomotion");
        }
        
        private void SetAnimatorIdle()
        {
            if (_animator == null) return;
            
            _animator.SetFloat("MoveSpeed", 0f);
            _animator.SetInteger("CurrentGait", 0);
            _animator.SetBool("IsStopped", true);
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("MovementInputPressed", false);
            _animator.SetTrigger("ForceIdle");
        }
        
        private void InspectAnimator()
        {
            if (_animator == null) return;
            
            Debug.Log("=== ANIMATOR INSPECTION ===");
            Debug.Log($"Controller: {_animator.runtimeAnimatorController?.name}");
            
            // Verificar todos los parámetros
            Debug.Log("Parameters:");
            foreach (var param in _animator.parameters)
            {
                string value = "N/A";
                
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        value = _animator.GetFloat(param.nameHash).ToString("F2");
                        break;
                    case AnimatorControllerParameterType.Int:
                        value = _animator.GetInteger(param.nameHash).ToString();
                        break;
                    case AnimatorControllerParameterType.Bool:
                        value = _animator.GetBool(param.nameHash).ToString();
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        value = "(trigger)";
                        break;
                }
                
                Debug.Log($"  - {param.name} ({param.type}): {value}");
            }
            
            // Verificar capas y estados
            for (int layer = 0; layer < _animator.layerCount; layer++)
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layer);
                string layerName = _animator.GetLayerName(layer);
                
                Debug.Log($"Layer {layer} ({layerName}):");
                Debug.Log($"  - Current State Hash: {stateInfo.shortNameHash}");
                Debug.Log($"  - Normalized Time: {stateInfo.normalizedTime:F2}");
                Debug.Log($"  - Speed: {stateInfo.speed:F2}");
                Debug.Log($"  - Tag: {stateInfo.tagHash}");
            }
        }
        
        private void OnGUI()
        {
            if (!_showDebugInfo) return;
            
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(10, 120, 300, 300));
            GUILayout.Label("Unit Animation Debug", GUI.skin.box);
            
            if (_animationAdapter != null)
            {
                GUI.color = Color.green;
                GUILayout.Label($"Animation Adapter:");
                GUI.color = Color.white;
                GUILayout.Label($"  Speed: {_animationAdapter.NormalizedSpeed:F2}");
                GUILayout.Label($"  Direction: {_animationAdapter.MovementVector}");
                GUILayout.Label($"  Is Stopped: {_animationAdapter.IsStopped}");
                GUILayout.Label($"  Is Running: {_animationAdapter.IsRunning}");
            }
            else
            {
                GUILayout.Label("Animation Adapter: NOT FOUND", GUI.skin.box);
            }
            
            if (_animator != null)
            {
                GUI.color = Color.green;
                GUILayout.Label($"Animator Parameters:");
                GUI.color = Color.white;
                
                // Mostrar parámetros clave del animator
                int gait = _animator.GetInteger(Animator.StringToHash("CurrentGait"));
                float moveSpeed = _animator.GetFloat(Animator.StringToHash("MoveSpeed"));
                bool isStopped = _animator.GetBool(Animator.StringToHash("IsStopped"));
                bool isGrounded = _animator.GetBool(Animator.StringToHash("IsGrounded"));
                
                GUILayout.Label($"  MoveSpeed: {moveSpeed:F2}");
                GUILayout.Label($"  CurrentGait: {gait} ({GaitToString(gait)})");
                GUILayout.Label($"  IsStopped: {isStopped}");
                GUILayout.Label($"  IsGrounded: {isGrounded}");
                
                // Mostrar estado actual del animator
                AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                GUILayout.Label($"  Current State: {GetStateShortName(state)}");
            }
            else
            {
                GUILayout.Label("Animator: NOT FOUND", GUI.skin.box);
            }
            
            GUILayout.EndArea();
        }
        
        private string GaitToString(int gait)
        {
            switch(gait)
            {
                case 0: return "Idle";
                case 1: return "Walk";
                case 2: return "Run";
                case 3: return "Sprint";
                default: return "Unknown";
            }
        }
        
        private string GetStateShortName(AnimatorStateInfo state)
        {
            // Intentar determinar el nombre del estado actual
            if (state.IsName("Base Layer.Fall.Falling"))
                return "Falling";
            else if (state.IsName("Base Layer.Locomotion Standing.Locomotion"))
                return "Locomotion";
            else if (state.IsName("Base Layer.Idle Standing.Idle_Standing"))
                return "Idle";
                
            return $"Unknown ({state.shortNameHash})";
        }
    }
}
