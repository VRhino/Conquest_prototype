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
    protected override void OnUpdate()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        foreach (var entity in SystemAPI.Query<Entity>().WithAll<HeroInputComponent, IsLocalPlayer>())
        {
            var input = new HeroInputComponent();

            // Gather directional input (XZ plane).
            if (keyboard != null)
            {
                float x = 0f;
                float z = 0f;
                if (keyboard.aKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed) z -= 1f;
                if (keyboard.wKey.isPressed) z += 1f;
                input.moveInput = new float2(x, z);

                input.isSprinting = keyboard.leftShiftKey.isPressed;
                input.isJumping = keyboard.spaceKey.isPressed;
                input.useSkill1 = keyboard.qKey.isPressed;
                input.useSkill2 = keyboard.eKey.isPressed;
                input.useUltimate = keyboard.rKey.isPressed;
            }

            if (mouse != null)
            {
                input.isAttacking = mouse.leftButton.isPressed;
            }

            bool interact = keyboard != null && keyboard.fKey.wasPressedThisFrame;

            // Write the captured values back to the entity component.
            SystemAPI.SetComponent(entity, input);

            if (SystemAPI.HasComponent<PlayerInteractionComponent>(entity))
            {
                var inter = SystemAPI.GetComponentRW<PlayerInteractionComponent>(entity);
                inter.ValueRW.interactPressed = interact;
            }
            else
            {
                SystemAPI.AddComponent(entity, new PlayerInteractionComponent
                {
                    interactPressed = interact
                });
            }
        }
    }
}
