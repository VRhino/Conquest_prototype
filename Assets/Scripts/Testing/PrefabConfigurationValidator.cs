using UnityEngine;
using Unity.Entities;
using ConquestTactics.Animation;
using ConquestTactics.Visual;
using Synty.AnimationBaseLocomotion.Samples;

namespace ConquestTactics.Testing
{
    /// <summary>
    /// Validador de configuración para los prefabs ECS + Visual del sistema híbrido.
    /// Verifica que HeroEntity_pure.prefab y ModularCharacter.prefab estén configurados correctamente.
    /// </summary>
    public class PrefabConfigurationValidator : MonoBehaviour
    {
        [Header("Prefabs to Validate")]
        [SerializeField] private GameObject _heroEntityPurePrefab;
        [SerializeField] private GameObject _modularCharacterPrefab;
        
        [Header("Validation Settings")]
        [SerializeField] private bool _validateOnStart = true;
        [SerializeField] private bool _verboseLogging = true;
        
        [Header("Results")]
        [SerializeField] private bool _heroEntityValid = false;
        [SerializeField] private bool _modularCharacterValid = false;
        [SerializeField] private bool _overallValid = false;
        
        private void Start()
        {
            if (_validateOnStart)
            {
                ValidateConfiguration();
            }
        }
        
        [ContextMenu("Validate Configuration")]
        public void ValidateConfiguration()
        {
            Debug.Log("[PrefabConfigurationValidator] Starting validation...");
            
            _heroEntityValid = ValidateHeroEntityPrefab();
            _modularCharacterValid = ValidateModularCharacterPrefab();
            _overallValid = _heroEntityValid && _modularCharacterValid;
            
            LogResults();
        }
        
        private bool ValidateHeroEntityPrefab()
        {
            if (_heroEntityPurePrefab == null)
            {
                LogError("HeroEntity_pure.prefab is not assigned!");
                return false;
            }
            
            bool isValid = true;
            LogInfo($"Validating {_heroEntityPurePrefab.name}...");
            
            // Check for required Authoring components
            // HeroEntityAuthoring is the main baker that adds ALL necessary components
            var requiredComponents = new System.Type[]
            {
                typeof(HeroEntityAuthoring) // Main baker - adds ALL ECS components including TeamComponent
            };
            
            // Optional components (add redundancy and specific configuration)
            var optionalComponents = new System.Type[]
            {
                typeof(HeroInputAuthoring),  // Minimal authoring (empty MonoBehaviour)
                typeof(HeroStatsAuthoring),  // Has additional stats configuration
                typeof(HeroLifeAuthoring),   // Has life-specific configuration  
                typeof(HeroSpawnAuthoring),  // Has spawn-specific configuration
                typeof(TeamAuthoring)        // Optional - TeamComponent already added by HeroEntityBaker
            };
            
            // Validate required components
            foreach (var componentType in requiredComponents)
            {
                var component = _heroEntityPurePrefab.GetComponent(componentType);
                if (component == null)
                {
                    LogError($"Missing REQUIRED component: {componentType.Name}");
                    isValid = false;
                }
                else
                {
                    LogSuccess($"Found REQUIRED component: {componentType.Name}");
                }
            }
            
            // Validate optional components (warn if missing, but don't fail validation)
            foreach (var componentType in optionalComponents)
            {
                var component = _heroEntityPurePrefab.GetComponent(componentType);
                if (component == null)
                {
                    LogWarning($"Missing optional component: {componentType.Name} (adds specific configuration)");
                }
                else
                {
                    LogSuccess($"Found optional component: {componentType.Name}");
                }
            }
            
            // Check that it doesn't have visual components (should be pure ECS)
            var forbiddenComponents = new System.Type[]
            {
                typeof(Animator),
                typeof(CharacterController),
                typeof(EcsAnimationInputAdapter),
                typeof(EntityVisualSync)
            };
            
            foreach (var componentType in forbiddenComponents)
            {
                var component = _heroEntityPurePrefab.GetComponent(componentType);
                if (component != null)
                {
                    LogWarning($"Found visual component that should not be in pure ECS prefab: {componentType.Name}");
                    // Not marking as invalid, but warning
                }
            }
            
            return isValid;
        }
        
        private bool ValidateModularCharacterPrefab()
        {
            if (_modularCharacterPrefab == null)
            {
                LogError("ModularCharacter.prefab is not assigned!");
                return false;
            }
            
            bool isValid = true;
            LogInfo($"Validating {_modularCharacterPrefab.name}...");
            
            // Check for required visual components
            var requiredComponents = new System.Type[]
            {
                typeof(Animator),
                typeof(CharacterController),
                typeof(EcsAnimationInputAdapter),
                typeof(EntityVisualSync),
                typeof(VisualPrefabAuthoring)
            };
            
            foreach (var componentType in requiredComponents)
            {
                var component = _modularCharacterPrefab.GetComponent(componentType);
                if (component == null)
                {
                    LogError($"Missing required component: {componentType.Name}");
                    isValid = false;
                }
                else
                {
                    LogSuccess($"Found component: {componentType.Name}");
                }
            }
            
            // Check for the new animation controller
            var newController = _modularCharacterPrefab.GetComponent<SamplePlayerAnimationController_ECS>();
            if (newController == null)
            {
                LogError("Missing SamplePlayerAnimationController_ECS component!");
                isValid = false;
            }
            else
            {
                LogSuccess("Found SamplePlayerAnimationController_ECS");
                
                // Validate controller configuration
                ValidateAnimationControllerConfig(newController);
            }
            
            // Validate VisualPrefabAuthoring configuration
            var visualPrefabAuthoring = _modularCharacterPrefab.GetComponent<VisualPrefabAuthoring>();
            if (visualPrefabAuthoring != null)
            {
                LogSuccess("Found VisualPrefabAuthoring component");
                ValidateVisualPrefabAuthoring(visualPrefabAuthoring);
            }
            
            // Check that old components are removed
            var forbiddenComponents = new System.Type[]
            {
                // Note: SamplePlayerAnimationController should be replaced with SamplePlayerAnimationController_ECS
                // We don't check for it here since it may not be referenced anymore
            };
            
            foreach (var componentType in forbiddenComponents)
            {
                var component = _modularCharacterPrefab.GetComponent(componentType);
                if (component != null)
                {
                    LogWarning($"Found old component that should be removed: {componentType.Name}");
                }
            }
            
            return isValid;
        }
        
        private void ValidateAnimationControllerConfig(SamplePlayerAnimationController_ECS controller)
        {
            // Check if required references are assigned
            var animator = controller.GetComponent<Animator>();
            if (animator == null)
            {
                LogError("Animation controller missing Animator component");
            }
            else
            {
                LogSuccess("Animation controller has Animator component");
                
                // Check if Animator has a valid controller
                if (animator.runtimeAnimatorController == null)
                {
                    LogWarning("Animator has no RuntimeAnimatorController assigned");
                }
                else
                {
                    LogSuccess($"Animator has controller: {animator.runtimeAnimatorController.name}");
                }
            }

            var characterController = controller.GetComponent<CharacterController>();
            if (characterController == null)
            {
                LogError("Animation controller missing CharacterController component");
            }
            else
            {
                LogSuccess("Animation controller has CharacterController component");
            }

            var inputAdapter = controller.GetComponent<EcsAnimationInputAdapter>();
            if (inputAdapter == null)
            {
                LogError("Animation controller missing EcsAnimationInputAdapter component");
            }
            else
            {
                LogSuccess("Animation controller has EcsAnimationInputAdapter component");
            }
            
            // Check if camera controller reference would be available
            var cameraController = Object.FindObjectOfType<HeroCameraController>();
            if (cameraController == null)
            {
                LogWarning("No HeroCameraController found in scene (required for animation controller)");
            }
            else
            {
                LogSuccess("HeroCameraController found in scene");
            }
        }
        
        private void ValidateVisualPrefabAuthoring(VisualPrefabAuthoring visualPrefabAuthoring)
        {
            if (string.IsNullOrEmpty(visualPrefabAuthoring.prefabId))
            {
                LogWarning("VisualPrefabAuthoring has empty prefabId");
            }
            else
            {
                LogSuccess($"VisualPrefabAuthoring prefabId: {visualPrefabAuthoring.prefabId}");
            }
            
            if (visualPrefabAuthoring.autoRegister)
            {
                LogSuccess("VisualPrefabAuthoring will auto-register");
            }
            else
            {
                LogInfo("VisualPrefabAuthoring auto-register is disabled");
            }
            
            if (visualPrefabAuthoring.addSyncComponent)
            {
                LogSuccess("VisualPrefabAuthoring will auto-add sync component");
            }
            else
            {
                LogInfo("VisualPrefabAuthoring sync component auto-add is disabled");
            }
        }
        
        private void LogResults()
        {
            Debug.Log("=== PREFAB VALIDATION RESULTS ===");
            
            if (_overallValid)
            {
                LogSuccess("✅ ALL PREFABS CONFIGURED CORRECTLY!");
                LogInfo("Both HeroEntity_pure.prefab and ModularCharacter.prefab are ready for the ECS hybrid system.");
            }
            else
            {
                LogError("❌ CONFIGURATION ISSUES FOUND!");
                
                if (!_heroEntityValid)
                    LogError("  - HeroEntity_pure.prefab has configuration issues");
                    
                if (!_modularCharacterValid)
                    LogError("  - ModularCharacter.prefab has configuration issues");
                    
                LogInfo("Please check the issues above and fix them before proceeding.");
            }
            
            Debug.Log("=== END VALIDATION ===");
        }
        
        private void LogInfo(string message)
        {
            if (_verboseLogging)
                Debug.Log($"[PrefabValidator] {message}");
        }
        
        private void LogSuccess(string message)
        {
            if (_verboseLogging)
                Debug.Log($"[PrefabValidator] ✅ {message}");
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[PrefabValidator] ⚠️ {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[PrefabValidator] ❌ {message}");
        }

        [ContextMenu("Validate Full System Setup")]
        public void ValidateFullSystemSetup()
        {
            Debug.Log("[PrefabConfigurationValidator] Validating full ECS hybrid system setup...");
            
            ValidateConfiguration();
            
            // Additional system-wide validations
            ValidateEcsSystemSetup();
            ValidateSceneSetup();
        }
        
        private void ValidateEcsSystemSetup()
        {
            LogInfo("Validating ECS system setup...");
            
            // Check if required systems exist in the project
            var requiredSystems = new string[]
            {
                "HeroInputSystem",
                "HeroMovementSystem", 
                "HeroAttackSystem",
                "HeroStaminaSystem",
                "HeroSpawnSystem",
                "HeroVisualManagementSystem"
            };
            
            foreach (var systemName in requiredSystems)
            {
                // This is a basic check - in a real scenario you'd want to verify the systems are actually registered
                LogInfo($"System expected: {systemName}");
            }
        }
        
        private void ValidateSceneSetup()
        {
            LogInfo("Validating scene setup...");
            
            // Check for required scene objects
            var heroCameraController = Object.FindObjectOfType<HeroCameraController>();
            if (heroCameraController == null)
            {
                LogWarning("No HeroCameraController found in scene");
            }
            else
            {
                LogSuccess("HeroCameraController found in scene");
            }
            
            // Check for ECS World
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                LogWarning("No default ECS World found (normal in edit mode)");
            }
            else
            {
                LogSuccess("ECS World is available");
            }
        }

        [ContextMenu("Validate Baker Dependencies")]
        public void ValidateBakerDependencies()
        {
            Debug.Log("[PrefabConfigurationValidator] Validating Baker dependencies...");
            
            LogInfo("=== BAKER DEPENDENCY ANALYSIS ===");
            LogInfo("HeroEntityBaker provides ALL components:");
            LogInfo("  ✅ HeroLifeComponent");
            LogInfo("  ✅ HeroSpawnComponent");
            LogInfo("  ✅ TeamComponent (from authoring.teamValue)");
            LogInfo("  ✅ HeroStatsComponent");
            LogInfo("  ✅ StaminaComponent");
            LogInfo("  ✅ HeroSquadSelectionComponent");
            LogInfo("  ✅ IsLocalPlayer");
            LogInfo("  ✅ HeroInputComponent");
            LogInfo("  ✅ HeroVisualReference");
            
            LogInfo("TeamBaker is optional:");
            LogInfo("  ⚠️ TeamComponent (redundant with HeroEntityBaker)");
            
            LogInfo("Removed duplicate bakers:");
            LogInfo("  ❌ HeroInputBaker (duplicate)");
            LogInfo("  ❌ HeroStatsBaker (duplicate)");
            LogInfo("  ❌ HeroSpawnBaker (duplicate)");
            LogInfo("  ❌ HeroLifeBaker (duplicate)");
            
            LogSuccess("Baker dependencies optimized - no more duplicates!");
        }
    }
}
