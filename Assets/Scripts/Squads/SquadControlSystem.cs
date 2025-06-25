using Unity.Entities;
using UnityEngine.InputSystem;

/// <summary>
/// Reads player hotkeys and writes the corresponding commands to the
/// <see cref="SquadInputComponent"/> of the active squad.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadControlSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool orderIssued = false;
        bool formationChanged = false;
        SquadOrderType newOrder = default;
        FormationType newFormation = default;

        if (keyboard.cKey.wasPressedThisFrame)
        {
            newOrder = SquadOrderType.FollowHero;
            orderIssued = true;
        }
        else if (keyboard.xKey.wasPressedThisFrame)
        {
            newOrder = SquadOrderType.HoldPosition;
            orderIssued = true;
        }
        else if (keyboard.vKey.wasPressedThisFrame)
        {
            newOrder = SquadOrderType.Attack;
            orderIssued = true;
        }

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            newFormation = FormationType.Line;
            formationChanged = true;
        }
        else if (keyboard.f2Key.wasPressedThisFrame)
        {
            newFormation = FormationType.Dispersed;
            formationChanged = true;
        }
        else if (keyboard.f3Key.wasPressedThisFrame)
        {
            newFormation = FormationType.Testudo;
            formationChanged = true;
        }
        else if (keyboard.f4Key.wasPressedThisFrame)
        {
            newFormation = FormationType.Wedge;
            formationChanged = true;
        }

        if (!orderIssued && !formationChanged)
            return;

        foreach (var input in SystemAPI.Query<RefRW<SquadInputComponent>>().WithAll<IsLocalPlayer>())
        {
            if (orderIssued)
                input.ValueRW.orderType = newOrder;

            if (formationChanged)
                input.ValueRW.desiredFormation = newFormation;

            input.ValueRW.hasNewOrder = true;
        }
    }
}
