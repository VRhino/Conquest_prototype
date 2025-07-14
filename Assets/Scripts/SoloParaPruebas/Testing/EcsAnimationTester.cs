using UnityEngine;
using ConquestTactics.Animation;
using Synty.AnimationBaseLocomotion.Samples;

namespace ConquestTactics.Testing
{
    /// <summary>
    /// Script simple para testear la adaptación del sistema de animaciones ECS.
    /// Proporciona información de debug y permite hacer pruebas básicas.
    /// </summary>
    public class EcsAnimationTester : MonoBehaviour
    {
        [Header("Components to Test")]
        [SerializeField] private EcsAnimationInputAdapter _inputAdapter;
        [SerializeField] private SamplePlayerAnimationController_ECS _animationController;
        
        [Header("Debug Settings")]
        [SerializeField] private bool _showDebugInfo = true;
        [SerializeField] private bool _logInputEvents = false;
        
        [Header("Testing")]
        [SerializeField] private bool _enableManualTesting = false;
        [SerializeField] private KeyCode _testWalkToggleKey = KeyCode.T;
        
        private void Start()
        {
            // Auto-find components if not assigned
            if (_inputAdapter == null)
                _inputAdapter = FindObjectOfType<EcsAnimationInputAdapter>();
                
            if (_animationController == null)
                _animationController = FindObjectOfType<SamplePlayerAnimationController_ECS>();
            
            // Subscribe to events if logging is enabled
            if (_logInputEvents && _inputAdapter != null)
            {
                _inputAdapter.onWalkToggled += () => Debug.Log("[EcsAnimationTester] Walk Toggled");
                _inputAdapter.onSprintActivated += () => Debug.Log("[EcsAnimationTester] Sprint Activated");
                _inputAdapter.onSprintDeactivated += () => Debug.Log("[EcsAnimationTester] Sprint Deactivated");
            }
        }
        
        private void Update()
        {
            if (_enableManualTesting)
            {
                HandleManualTesting();
            }
        }
        
        private void HandleManualTesting()
        {
            if (Input.GetKeyDown(_testWalkToggleKey) && _inputAdapter != null)
            {
                _inputAdapter.TriggerWalkToggle();
                Debug.Log("[EcsAnimationTester] Manual walk toggle triggered");
            }
        }
        
        private void OnGUI()
        {
            if (!_showDebugInfo) return;
            
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(10, 10, 300, 300)); // Ampliar el área para más información
            GUILayout.Label("ECS Animation System Debug", GUI.skin.box);
            
            if (_inputAdapter != null)
            {
                GUILayout.Label($"Input Adapter Status:");
                GUILayout.Label($"  Move: {_inputAdapter._moveComposite}");
                GUILayout.Label($"  Movement Detected: {_inputAdapter._movementInputDetected}");
                GUILayout.Label($"  Duration: {_inputAdapter._movementInputDuration:F2}s");
            }
            else
            {
                GUILayout.Label("Input Adapter: NOT FOUND", GUI.skin.box);
            }
            
            if (_animationController != null)
            {
                GUILayout.Label($"Animation Controller:");
                // Mostrar más información detallada
                float speed = GetControllerSpeed();
                bool isStopped = IsControllerStopped();
                Vector3 velocity = GetControllerVelocity();
                string animState = GetAnimatorStateName();
                
                GUI.color = speed > 0.01f ? (isStopped ? Color.yellow : Color.green) : Color.white;
                GUILayout.Label($"  Speed: {speed:F3}");
                GUILayout.Label($"  Is Stopped: {isStopped}");
                GUILayout.Label($"  Velocity: {velocity:F3}");
                GUILayout.Label($"  Y Position: {_animationController.transform.position.y:F3}");
                GUILayout.Label($"  Anim State: {animState}");
                
                // Si hay deslizamiento, mostrar advertencia
                if (speed > 0.01f && _inputAdapter._moveComposite.sqrMagnitude < 0.01f)
                {
                    GUI.color = Color.red;
                    GUILayout.Label("⚠️ DESLIZAMIENTO DETECTADO ⚠️");
                }
                
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label("Animation Controller: NOT FOUND", GUI.skin.box);
            }
            
            if (_enableManualTesting)
            {
                GUILayout.Label($"Manual Testing Enabled");
                GUILayout.Label($"Press {_testWalkToggleKey} to toggle walk");
            }
            
            GUILayout.EndArea();
        }
        
        // AÑADIDO: Método para obtener la velocidad del controlador
        private float GetControllerSpeed()
        {
            if (_animationController == null) return 0f;
            
            // Usar reflexión para acceder al campo privado _speed2D
            System.Reflection.FieldInfo fieldInfo = 
                typeof(SamplePlayerAnimationController_ECS).GetField("_speed2D", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
            if (fieldInfo != null)
            {
                return (float)fieldInfo.GetValue(_animationController);
            }
            
            return 0f;
        }
        
        // AÑADIDO: Método para verificar si el controlador está detenido
        private bool IsControllerStopped()
        {
            if (_animationController == null) return true;
            
            System.Reflection.FieldInfo fieldInfo = 
                typeof(SamplePlayerAnimationController_ECS).GetField("_isStopped", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
            if (fieldInfo != null)
            {
                return (bool)fieldInfo.GetValue(_animationController);
            }
            
            return true;
        }
        
        // Método para obtener la velocidad del controlador
        private Vector3 GetControllerVelocity()
        {
            if (_animationController == null) return Vector3.zero;
            
            // Usar reflexión para acceder al campo privado _velocity
            System.Reflection.FieldInfo fieldInfo = 
                typeof(SamplePlayerAnimationController_ECS).GetField("_velocity", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
            if (fieldInfo != null)
            {
                return (Vector3)fieldInfo.GetValue(_animationController);
            }
            
            return Vector3.zero;
        }
        
        // Método mejorado para obtener el nombre del estado del Animator con detección más completa
        private string GetAnimatorStateName()
        {
            if (_animationController == null) return "Unknown";
            
            Animator animator = _animationController.GetComponent<Animator>();
            if (animator == null) return "No Animator";
            
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            int stateHash = stateInfo.fullPathHash;
            
            // Intentar nombres completos primero (cobertura exhaustiva)
            if (stateInfo.IsName("Base Layer.Fall.Falling"))
                return "Falling";
            else if (stateInfo.IsName("Base Layer.Locomotion Standing.Locomotion"))
                return "Locomotion";
            else if (stateInfo.IsName("Base Layer.Idle Standing.Idle_Standing"))
                return "Idle";
            else if (stateInfo.IsName("Base Layer.Shuffle Standing.TurnInPlace_Standing"))
                return "Turn In Place";
            else if (stateInfo.IsName("Base Layer.Landing.Landing"))
                return "Landing";
            else if (stateInfo.IsName("Base Layer.Shuffle Standing.ShuffleStart_Standing"))
                return "Shuffle Start";
            else if (stateInfo.IsName("Base Layer.Jump.Jumping"))
                return "Jumping";
            else if (stateInfo.IsName("Base Layer.Locomotion Crouching.Crouch_Locomotion"))
                return "Crouch Locomotion";
            else if (stateInfo.IsName("Base Layer.Idle Crouching.Crouch_Idle"))
                return "Crouch Idle";
            
            // Si no se encuentra con path completo, intentar por tags
            else if (stateInfo.IsTag("Fall"))
                return "Fall (Tag)";
            else if (stateInfo.IsTag("Locomotion"))
                return "Locomotion (Tag)";
            else if (stateInfo.IsTag("Idle"))
                return "Idle (Tag)";
            else if (stateInfo.IsTag("Jump"))
                return "Jump (Tag)";
            else if (stateInfo.IsTag("Crouch"))
                return "Crouch (Tag)";
            
            // Si sigue siendo desconocido, analizar la información adicional
            float normalizedTime = stateInfo.normalizedTime;
            bool isGrounded = animator.GetBool("IsGrounded");
            bool isStopped = animator.GetBool("IsStopped");
            
            // Intentar inferir el estado basado en otros parámetros
            string stateDetails = $"Hash:{stateHash}, NT:{normalizedTime:F2}";
            if (isGrounded && isStopped)
                return $"Prob. Idle ({stateDetails})";
            else if (isGrounded && !isStopped)
                return $"Prob. Locomotion ({stateDetails})";
            else if (!isGrounded)
                return $"Prob. Air State ({stateDetails})";
                
            return $"Unknown ({stateDetails})";
        }
        
        [ContextMenu("Test Walk Toggle")]
        public void TestWalkToggle()
        {
            if (_inputAdapter != null)
            {
                _inputAdapter.TriggerWalkToggle();
            }
        }
        
        [ContextMenu("Debug Input State")]
        public void DebugInputState()
        {
            if (_inputAdapter != null)
            {
                _inputAdapter.DebugInputState();
            }
        }
        
        // AÑADIDO: Método de test para verificar la detención inmediata
        [ContextMenu("Test Stop Detection")]
        public void TestStopDetection()
        {
            StartCoroutine(StopDetectionTest());
        }
        
        private System.Collections.IEnumerator StopDetectionTest()
        {
            Debug.Log("[EcsAnimationTester] Iniciando test de detención inmediata...");
            
            // Primera medición
            float initialSpeed = GetControllerSpeed();
            bool initialStopped = IsControllerStopped();
            Debug.Log($"[EcsAnimationTester] Estado inicial - Velocidad: {initialSpeed:F3}, Detenido: {initialStopped}");
            
            // Esperar un frame
            yield return null;
            
            // Segunda medición
            float currentSpeed = GetControllerSpeed();
            bool currentStopped = IsControllerStopped();
            Debug.Log($"[EcsAnimationTester] Estado actual - Velocidad: {currentSpeed:F3}, Detenido: {currentStopped}");
            
            // Esperar 0.5 segundos
            yield return new WaitForSeconds(0.5f);
            
            // Medición final
            float finalSpeed = GetControllerSpeed();
            bool finalStopped = IsControllerStopped();
            Debug.Log($"[EcsAnimationTester] Estado final - Velocidad: {finalSpeed:F3}, Detenido: {finalStopped}");
            
            Debug.Log("[EcsAnimationTester] Test de detención completado.");
        }
    }
}
