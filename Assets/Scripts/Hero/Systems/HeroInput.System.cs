using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

/// <summary>
/// System that captures keyboard and mouse input using the Unity Input System
/// and writes the result to <see cref="HeroInputComponent"/>. Only runs for
/// the entity tagged with <see cref="IsLocalPlayer"/>.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroInputSystem : SystemBase
{
    
    private bool uiInteractionState;
    [WithAll(typeof(IsLocalPlayer))]
    partial struct HeroInputJob : IJobEntity
    {
        public float2 moveInput;
        public bool isSprinting;
        public bool useSkill1;
        public bool useSkill2;
        public bool useUltimate;
        public bool isAttacking;
        public bool interactPressed;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<PlayerInteractionComponent> interactionLookup;

        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref HeroInputComponent input)
        {
            input.MoveInput = moveInput;
            input.IsSprintPressed = isSprinting;
            input.UseSkill1 = useSkill1;
            input.UseSkill2 = useSkill2;
            input.UseUltimate = useUltimate;
            input.IsAttackPressed = isAttacking;

            if (interactionLookup.HasComponent(entity))
            {
                var inter = interactionLookup[entity];
                inter.interactPressed = interactPressed;
                interactionLookup[entity] = inter;
            }
            else
            {
                ecb.AddComponent(sortKey, entity, new PlayerInteractionComponent
                {
                    interactPressed = interactPressed
                });
            }
        }
    }

    protected override void OnUpdate()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        float2 move = float2.zero;
        bool sprint = false;
        bool skill1 = false;
        bool skill2 = false;
        bool ultimate = false;
        bool attack = false;
        bool interact = false;

        // Solo loguea si hay input relevante
        bool hasInput = false;
        UnityEngine.Debug.Log($"[HeroInputSystem] IsDialogueOpen: {DialogueUIState.IsDialogueOpen}");
        if (keyboard != null && DialogueUIState.IsDialogueOpen == false)
        {
            if (keyboard.aKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.wKey.isPressed) move.y += 1f;
            sprint = keyboard.leftShiftKey.isPressed;
            skill1 = keyboard.qKey.isPressed;
            skill2 = keyboard.eKey.isPressed;
            ultimate = keyboard.rKey.isPressed;
            interact = keyboard.fKey.wasPressedThisFrame;
        }

        if (mouse != null)
        {
            attack = mouse.leftButton.isPressed;
        }

        hasInput = (move.x != 0 || move.y != 0 || sprint || skill1 || skill2 || ultimate || attack || interact);

        // --- Abrir/cerrar inventario y hero detail solo en la escena del feudo ---

        manageUIDisplays();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        var job = new HeroInputJob
        {
            moveInput = move,
            isSprinting = sprint,
            useSkill1 = skill1,
            useSkill2 = skill2,
            useUltimate = ultimate,
            isAttacking = attack,
            interactPressed = interact,
            interactionLookup = GetComponentLookup<PlayerInteractionComponent>(),
            ecb = ecb.AsParallelWriter()
        };

        var handle = job.ScheduleParallel(Dependency);
        Dependency = handle;

        handle.Complete(); // Solo si necesitas efectos inmediatos, como el ecb.Playback
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void manageUIDisplays()
    {
        var keyboardInput = UnityEngine.InputSystem.Keyboard.current;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isFeudoScene = currentScene == "FeudoScene";

        if (!isFeudoScene || keyboardInput == null) return;

        // Verificar si el FullscreenPanelManager está disponible
        if (FullscreenPanelManager.Instance == null)
        {
            UnityEngine.Debug.LogWarning("[HeroInputSystem] FullscreenPanelManager no está disponible");
            return;
        }

        // Manejo del inventario con tecla 'I'
        if (keyboardInput.iKey.wasPressedThisFrame) FullscreenPanelManager.Instance.HandleInventoryKeyPress();

        // Manejo del Hero Detail con tecla 'P'
        if (keyboardInput.pKey.wasPressedThisFrame) FullscreenPanelManager.Instance.HandleHeroDetailKeyPress();

        // Manejo de las Barracas con tecla 'B'
        if (keyboardInput.bKey.wasPressedThisFrame) FullscreenPanelManager.Instance.HandleBarracksKeyPress();

        if (keyboardInput.escapeKey.wasPressedThisFrame) FullscreenPanelManager.Instance.HandleEscapeKeyPress();

        if (keyboardInput.altKey.isPressed)
        {
            uiInteractionState = !uiInteractionState;
            FullscreenPanelManager.Instance.SetUIInteractionState(uiInteractionState);
        }
    }
}
