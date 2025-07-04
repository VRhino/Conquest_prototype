using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ConquestTactics.Visual;

namespace ConquestTactics.Animation
{
    /// <summary>
    /// Script para forzar animaciones en todas las unidades - útil para corregir unidades atascadas
    /// Este script debe agregarse a un GameObject en la escena y puede activarse con las teclas configuradas
    /// </summary>
    public class UnitAnimationDebugger : MonoBehaviour
    {
        [Header("Key Bindings")]
        [Tooltip("Tecla para forzar animación de movimiento en todas las unidades")]
        [SerializeField] private KeyCode _forceMovementKey = KeyCode.F7;
        
        [Tooltip("Tecla para forzar animación de idle en todas las unidades")]
        [SerializeField] private KeyCode _forceIdleKey = KeyCode.F8;
        
        [Tooltip("Tecla para reiniciar todos los animadores")]
        [SerializeField] private KeyCode _resetAnimatorsKey = KeyCode.F9;
        
        [Header("Debug Settings")]
        [Tooltip("Mostrar información en consola")]
        [SerializeField] private bool _enableLogs = true;
        
        private void Update()
        {
            // Forzar animación de movimiento en todas las unidades
            if (Input.GetKeyDown(_forceMovementKey))
            {
                ForceAllUnitsMovement();
            }
            
            // Forzar animación de idle en todas las unidades
            if (Input.GetKeyDown(_forceIdleKey))
            {
                ForceAllUnitsIdle();
            }
            
            // Reiniciar todos los animadores
            if (Input.GetKeyDown(_resetAnimatorsKey))
            {
                ResetAllAnimators();
            }
        }
        
        /// <summary>
        /// Fuerza animación de movimiento en todas las unidades
        /// </summary>
        public void ForceAllUnitsMovement()
        {
            var controllers = FindObjectsOfType<UnitAnimatorController>();
            
            if (_enableLogs)
            {
                Debug.Log($"[UnitAnimationDebugger] Forzando animación de MOVIMIENTO en {controllers.Length} unidades");
            }
            
            foreach (var controller in controllers)
            {
                controller.ForceMovementAnimation();
            }
        }
        
        /// <summary>
        /// Fuerza animación de idle en todas las unidades
        /// </summary>
        public void ForceAllUnitsIdle()
        {
            var controllers = FindObjectsOfType<UnitAnimatorController>();
            
            if (_enableLogs)
            {
                Debug.Log($"[UnitAnimationDebugger] Forzando animación de IDLE en {controllers.Length} unidades");
            }
            
            foreach (var controller in controllers)
            {
                controller.ForceIdleAnimation();
            }
        }
        
        /// <summary>
        /// Reinicia todos los animadores
        /// </summary>
        public void ResetAllAnimators()
        {
            var animators = FindObjectsOfType<Animator>();
            
            if (_enableLogs)
            {
                Debug.Log($"[UnitAnimationDebugger] Reiniciando {animators.Length} animadores");
            }
            
            foreach (var animator in animators)
            {
                // Rebind reinicia todo el estado del animador
                animator.Rebind();
                animator.Update(0);
            }
        }
    }
}
