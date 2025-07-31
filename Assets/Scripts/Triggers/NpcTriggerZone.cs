using UnityEngine;
using ConquestTactics.UI;

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
            // var hero = PlayerSessionService.SelectedHero;
            // if (hero == null) return;
            // if (buildingID == "barracks")
            //     BarrackMenuUI.Open(hero);
            // else if (buildingID == "armory")
            //     ArmoryMenuUI.Open(hero);
            // // Aquí puedes agregar más edificios si es necesario
            // // Registrar flag de activación en memoria (puede ser un servicio estático)
            // NpcTriggerActivationFlags.RegisterActivation(buildingID);
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
    }
}
