using UnityEngine;
using Unity.Entities;
using ConquestTactics.Visual;
using ConquestTactics.UI;

namespace ConquestTactics.Interactables
{
    [RequireComponent(typeof(Collider))]
    public class DoorInteractable : MonoBehaviour
    {
        [Header("Door Setup")]
        [SerializeField] private Animator _doorAnimator;

        [Header("Interaction Prompt")]
        [SerializeField] private NpcPromptBillboard _promptBillboard;

        private const string OpenTrigger  = "Open";
        private const string CloseTrigger = "Close";
        private const string TextOpen     = "Presiona F para abrir";
        private const string TextClose    = "Presiona F para cerrar";

        private bool _isOpen       = false;
        private bool _playerInside = false;
        private GameObject _playerGO;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsLocalHero(other)) return;
            _playerInside = true;
            _playerGO = other.gameObject;
            ShowPrompt();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsLocalHero(other)) return;
            _playerInside = false;
            _playerGO = null;
            HidePrompt();
        }

        private void Update()
        {
            if (!_playerInside || _playerGO == null) return;

            var visualSync = _playerGO.GetComponent<EntityVisualSync>();
            if (visualSync == null || !visualSync.IsLocalHero) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var entity = visualSync.GetHeroEntity();
            var em = world.EntityManager;
            if (!em.HasComponent<PlayerInteractionComponent>(entity)) return;

            if (em.GetComponentData<PlayerInteractionComponent>(entity).interactPressed)
                ToggleDoor();
        }

        private void ToggleDoor()
        {
            _isOpen = !_isOpen;
            _doorAnimator?.SetTrigger(_isOpen ? OpenTrigger : CloseTrigger);
            ShowPrompt();
        }

        private void ShowPrompt()
        {
            string text = _isOpen ? TextClose : TextOpen;
            if (_promptBillboard != null)
            {
                _promptBillboard.gameObject.SetActive(true);
                _promptBillboard.SetPromptText(text);
            }
            else
            {
                NpcTriggerUIController.ShowInteractionPrompt(text, this);
            }
        }

        private void HidePrompt()
        {
            if (_promptBillboard != null)
                _promptBillboard.gameObject.SetActive(false);
            else
                NpcTriggerUIController.HideInteractionPrompt(this);
        }

        private static bool IsLocalHero(Collider other)
        {
            var vs = other.GetComponent<EntityVisualSync>();
            return vs != null && vs.IsLocalHero;
        }
    }
}
