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
    /// <summary>The left button went down for the UI; it does not attack until it is released (H10).</summary>
    private bool uiOwnsClick;
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
        bool squad1 = false;
        bool squad2 = false;
        bool squad3 = false;
        bool ultimate = false;
        bool attack = false;
        bool interact = false;
        bool mount = false;
        var uiSquadSkill = BattleCommandInput.ConsumeSquadSkill();

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
            ultimate = keyboard.tKey.isPressed;
            perk1 = keyboard.digit4Key.isPressed;
            perk2 = keyboard.digit5Key.isPressed;
            squad1 = keyboard.digit1Key.isPressed; // Squad abilities on 1/2/3 (GDD 12.3), active perks on 4/5.
            squad2 = keyboard.digit2Key.isPressed;
            squad3 = keyboard.digit3Key.isPressed;
            interact = keyboard.fKey.wasPressedThisFrame; // The ultimate is on T so F stays for interacting.
            mount = keyboard.gKey.wasPressedThisFrame;
        }

        if (mouse != null && DialogueUIState.IsDialogueOpen == false)
        {
            uiOwnsClick = mouse.leftButton.isPressed && (uiOwnsClick || BattleCommandInput.PointerMode);
            attack = mouse.leftButton.isPressed && !uiOwnsClick;
        }

        // A UI request is one frame of the key it stands for, so it follows the same path to SkillGate (H10).
        if (DialogueUIState.IsDialogueOpen == false)
        {
            squad1 |= uiSquadSkill == SkillSlot.Squad1;
            squad2 |= uiSquadSkill == SkillSlot.Squad2;
            squad3 |= uiSquadSkill == SkillSlot.Squad3;
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
            input.ValueRW.UseSquadAbility1 = squad1;
            input.ValueRW.UseSquadAbility2 = squad2;
            input.ValueRW.UseSquadAbility3 = squad3;
            input.ValueRW.UseUltimate = ultimate;
            input.ValueRW.IsAttackPressed = attack;
            input.ValueRW.MountPressed = mount;

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

        // Perks del loadout con tecla 'K' (H5)
        if (keyboardInput.kKey.wasPressedThisFrame) FullscreenPanelManager.Instance.HandlePerksKeyPress();

        if (keyboardInput.escapeKey.wasPressedThisFrame) FullscreenPanelManager.Instance.HandleEscapeKeyPress();

        if (keyboardInput.altKey.wasPressedThisFrame)
        {
            uiInteractionState = !uiInteractionState;
            FullscreenPanelManager.Instance.SetUIInteractionState(uiInteractionState);
        }
    }
}
