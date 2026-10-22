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
    protected override void OnUpdate()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        float2 move = float2.zero;
        bool sprint = false;
        bool skill1 = false;
        bool skill2 = false;
        bool skill3 = false;
        bool perk1 = false;
        bool perk2 = false;
        bool ultimate = false;
        bool attack = false;
        bool interact = false;

        if (keyboard != null && DialogueUIState.IsDialogueOpen == false)
        {
            if (keyboard.aKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.wKey.isPressed) move.y += 1f;
            sprint = keyboard.leftShiftKey.isPressed;
            skill1 = keyboard.qKey.isPressed;
            skill2 = keyboard.eKey.isPressed;
            skill3 = keyboard.rKey.isPressed;
            ultimate = keyboard.fKey.isPressed;
            perk1 = keyboard.digit1Key.isPressed;
            perk2 = keyboard.digit2Key.isPressed;
            interact = keyboard.gKey.wasPressedThisFrame; // F is the ultimate (GDD); interacting moved to G.
        }

        if (mouse != null && DialogueUIState.IsDialogueOpen == false)
        {
            attack = mouse.leftButton.isPressed;
        }

        // --- Abrir/cerrar inventario y hero detail solo en la escena del feudo ---

        manageUIDisplays();
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (input, entity) in SystemAPI.Query<RefRW<HeroInputComponent>>()
                     .WithAll<IsLocalPlayer>().WithEntityAccess())
        {
            input.ValueRW.MoveInput = move;
            input.ValueRW.IsSprintPressed = sprint;
            input.ValueRW.UseSkill1 = skill1;
            input.ValueRW.UseSkill2 = skill2;
            input.ValueRW.UseSkill3 = skill3;
            input.ValueRW.UsePerk1 = perk1;
            input.ValueRW.UsePerk2 = perk2;
            input.ValueRW.UseUltimate = ultimate;
            input.ValueRW.IsAttackPressed = attack;

            if (SystemAPI.HasComponent<PlayerInteractionComponent>(entity))
            {
                var interaction = SystemAPI.GetComponentRW<PlayerInteractionComponent>(entity);
                interaction.ValueRW.interactPressed = interact;
            }
            else
                ecb.AddComponent(entity, new PlayerInteractionComponent { interactPressed = interact });
        }
        ecb.Playback(EntityManager);
    }

    private void manageUIDisplays()
    {
        var keyboardInput = UnityEngine.InputSystem.Keyboard.current;
        
        var gameStateQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (gameStateQuery.IsEmpty) return;
        var gameState = EntityManager.GetComponentData<GameStateComponent>(gameStateQuery.GetSingletonEntity());
        bool isFeudoPhase = gameState.currentPhase == GamePhase.Feudo;

        if (!isFeudoPhase || keyboardInput == null) return;

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

        if (keyboardInput.altKey.wasPressedThisFrame)
        {
            uiInteractionState = !uiInteractionState;
            FullscreenPanelManager.Instance.SetUIInteractionState(uiInteractionState);
        }
    }
}
