using Unity.Mathematics;

/// <summary>One frame of local squad controls, independent of the input device.</summary>
public struct SquadControlCommand
{
    public bool orderIssued;
    public SquadOrderType order;
    public float3 holdPosition;
    public bool toggleHurry;
    public bool formationRequested;
    public int formationIndex;
    public bool cycleFormation;
}

/// <summary>Applies a local command without reading input or mutating ECS directly.</summary>
public static class SquadPlayerCommandResolver
{
    private const int InsistenceCount = 3;
    private const float InsistenceWindow = 0.8f;
    private const float CooldownDuration = 4f;

    /// <summary>
    /// Applies <paramref name="command"/> at simulation time <paramref name="now"/>; the insistence window and the
    /// override are stored as end times (L3-05), so nothing has to count them down.
    /// </summary>
    public static bool TryApply(SquadControlCommand command,
        SquadDefinitionComponent definition, ref SquadInputComponent input,
        ref SquadPlayerOrderIntentComponent intent, double now)
    {
        FormationType selectedFormation = intent.desiredFormation;
        bool validFormationChange = command.formationRequested
            && TryResolveFormation(definition, intent.desiredFormation,
                command.formationIndex, command.cycleFormation, out selectedFormation);
        if (!command.orderIssued && !validFormationChange && !command.toggleHurry)
            return false;

        if (command.toggleHurry)
            input.hurryToComander = !input.hurryToComander;

        if (command.orderIssued)
        {
            if (command.order == intent.orderType && now < intent.insistenceEndsAt)
                intent.insistenceCount++;
            else
            {
                intent.insistenceCount = 1;
                intent.insistenceEndsAt = now + InsistenceWindow;
            }

            if (intent.insistenceCount >= InsistenceCount && now >= intent.overrideEndsAt)
            {
                intent.overrideEndsAt = now + CooldownDuration;
                intent.insistenceCount = 0;
            }

            intent.orderType = command.order;
            intent.holdPosition = command.holdPosition;
            if (command.order == SquadOrderType.FollowHero)
            {
                // A single follow command releases defensive combat and rank posture, and the squad stands at rest
                // («En reposo»); a squad without a rest formation opens up into Dispersed if it has one.
                intent.overrideEndsAt = now + CooldownDuration;
                if (definition.formationLibrary.IsCreated)
                {
                    ref var library = ref definition.formationLibrary.Value;
                    if (FormationLayout.Find(ref library, FormationType.Rest) >= 0)
                        intent.desiredFormation = FormationType.Rest;
                    else if (FormationLayout.Find(ref library, FormationType.Dispersed) >= 0)
                        intent.desiredFormation = FormationType.Dispersed;
                }
            }
        }

        if (validFormationChange)
            intent.desiredFormation = selectedFormation;
        input.hasNewOrder |= command.orderIssued;
        return true;
    }

    /// <summary>
    /// F1-F4 (<paramref name="requestedIndex"/>) or the X cycle over the squad's formations. The rest formation is never
    /// one of them: it is baked last and only FollowHero asks for it.
    /// </summary>
    public static bool TryResolveFormation(SquadDefinitionComponent definition,
        FormationType current, int requestedIndex, bool cycle, out FormationType selected)
    {
        selected = current;
        if (!definition.formationLibrary.IsCreated)
            return false;

        ref var formations = ref definition.formationLibrary.Value.formations;
        int count = formations.Length;
        if (count > 0 && formations[count - 1].formationType == FormationType.Rest)
            count--;
        if (count == 0)
            return false;

        int index = requestedIndex;
        if (cycle)
        {
            // From rest (or an unknown formation) the cycle starts again at the first one.
            index = -1;
            for (int i = 0; i < count; i++)
                if (formations[i].formationType == current)
                {
                    index = i;
                    break;
                }
            index = (index + 1) % count;
        }
        if (index < 0 || index >= count)
            return false;

        selected = formations[index].formationType;
        return true;
    }
}
