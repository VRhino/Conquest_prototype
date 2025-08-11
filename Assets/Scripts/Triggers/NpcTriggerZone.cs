using ConquestTactics.UI;
using ConquestTactics.Dialogue;
using UnityEngine;

namespace ConquestTactics.Triggers
{
    /// <summary>
    /// Zona de activación para interacción con NPCs (barracón, armería, etc).
    /// Muestra un prompt de interacción y permite abrir el menú correspondiente al presionar E.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NpcTriggerZone : MonoBehaviour
    {
        [Tooltip("ID del edificio asociado a este trigger (ej: 'barracks', 'armory')")]
        public string buildingID;
        private bool _playerInside = false;
        private GameObject _playerObject;
        private string _promptText = "Presiona 'F' para interactuar";



        private void Reset()
        {
            // Asegura que el collider es trigger
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsHeroPlayer(other))
            {
                if (TutorialAllowsInteraction())
                {
                    _playerInside = true;
                    _playerObject = other.gameObject;
                    NpcTriggerUIController.ShowInteractionPrompt(_promptText, this);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsHeroPlayer(other))
            {
                _playerInside = false;
                _playerObject = null;
                NpcTriggerUIController.HideInteractionPrompt(this);
            }
        }

        private void Update()
        {
            // Usar el input ECS: si el jugador local está dentro y presionó interactuar
            if (_playerInside && _playerObject != null)
            {
                var visualSync = _playerObject.GetComponent<ConquestTactics.Visual.EntityVisualSync>();
                if (visualSync != null && visualSync.IsLocalHero)
                {
                    // Obtener la entidad ECS asociada
                    var entity = visualSync.GetHeroEntity();
                    // Obtener el EntityManager
                    var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                    if (world != null && world.IsCreated)
                    {
                        var entityManager = world.EntityManager;
                        if (entityManager.HasComponent<PlayerInteractionComponent>(entity))
                        {
                            var interaction = entityManager.GetComponentData<PlayerInteractionComponent>(entity);
                            if (interaction.interactPressed && TutorialAllowsInteraction())
                            {
                                Interact();
                            }
                        }
                    }
                }
            }
        }

        public void Interact()
        {
            Debug.Log($"[NpcTriggerZone] Interact called for building: {buildingID}");
            // Buscar el NpcDialogueReference en el NPC (este GameObject o padres)
            var dialogueRef = GetComponentInParent<NpcDialogueReference>() ?? GetComponent<NpcDialogueReference>();
            if (dialogueRef == null || dialogueRef.dialogueData == null)
            {
                Debug.LogWarning($"[NpcTriggerZone] No se encontró NpcDialogueReference o dialogueData en {gameObject.name}");
                return;
            }
            // Marcar diálogo abierto y pausar cámara
            OpenDialogue();

            // Abrir el menú de diálogo
            NpcDialogueUIController.Instance.Open(dialogueRef.dialogueData, OnDialogueOptionSelected);

            // Callback para manejar la opción seleccionada en el diálogo
            void OnDialogueOptionSelected(Dialogue.DialogueOption option)
            {
                // Al cerrar el diálogo, reanudar cámara y limpiar flag

                switch (option.optionType)
                {
                    case Dialogue.DialogueOptionType.OpenMenu:
                        if (option.nextMenuId == "barracks")
                        {
                            var heroData = PlayerSessionService.SelectedHero;
                            NpcTriggerUIController.Instance?.OpenBarracksMenu(heroData);
                        }
                        break;
                        
                    case Dialogue.DialogueOptionType.CloseDialogue:
                        CloseDialogue();
                        break;
                        
                    case Dialogue.DialogueOptionType.ExecuteEffects:
                        var hero = PlayerSessionService.SelectedHero;
                        if (hero != null && option.dialogueEffectIds != null && option.dialogueEffectIds.Length > 0)
                        {
                            InventoryService.Initialize(hero);
                            
                            Debug.Log($"[NpcTriggerZone] Executing {option.dialogueEffectIds.Length} dialogue effects...");
                            
                            // Ejecutar los efectos usando DialogueEffectSystem
                            bool allSucceeded = ConquestTactics.Dialogue.DialogueEffectSystem.ExecuteDialogueEffects(
                                option.dialogueEffectIds, 
                                hero, 
                                buildingID, 
                                option.effectParameters
                            );
                            
                            if (allSucceeded)
                            {
                                Debug.Log($"[NpcTriggerZone] All effects executed successfully");
                            }
                            else
                            {
                                Debug.LogWarning($"[NpcTriggerZone] Some effects failed to execute");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[NpcTriggerZone] ExecuteEffects called but no hero or effect IDs available");
                        }
                        CloseDialogue();
                        break;
                }
            }
        }

        private bool IsHeroPlayer(Collider other)
        {
            // Detecta si el GameObject tiene un EntityVisualSync marcado como IsLocalHero
            var visualSync = other.GetComponent<ConquestTactics.Visual.EntityVisualSync>();
            return visualSync != null && visualSync.IsLocalHero;
        }

        private bool TutorialAllowsInteraction()
        {
            // // Consulta el sistema de flags de tutorial
            // return TutorialFlags.Allows(buildingID);
            return true; // Por defecto, permite interacción
        }

        private void OpenDialogue()
        {
            DialogueUIState.IsDialogueOpen = true;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(false);
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseDialogue()
        {
            DialogueUIState.IsDialogueOpen = false;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(true);
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
