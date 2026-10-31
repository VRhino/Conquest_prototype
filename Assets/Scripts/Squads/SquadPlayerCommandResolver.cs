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

    public static bool TryApply(SquadControlCommand command,
        SquadDefinitionComponent definition, ref SquadInputComponent input,
        ref SquadPlayerOrderIntentComponent intent)
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
            if (command.order == intent.orderType && intent.insistenceTimer > 0f)
                intent.insistenceCount++;
            else
            {
                intent.insistenceCount = 1;
                intent.insistenceTimer = InsistenceWindow;
            }

            if (intent.insistenceCount >= InsistenceCount && !intent.heroOrdenCooldownActive)
            {
                intent.heroOrdenCooldownActive = true;
                intent.heroOrdenCooldownTimer = CooldownDuration;
                intent.insistenceCount = 0;
            }

            intent.orderType = command.order;
            intent.holdPosition = command.holdPosition;
            if (command.order == SquadOrderType.FollowHero)
            {
                // A single follow command releases defensive combat and rank posture.
                intent.heroOrdenCooldownActive = true;
                intent.heroOrdenCooldownTimer = CooldownDuration;
                if (definition.formationLibrary.IsCreated)
                {
                    ref var library = ref definition.formationLibrary.Value;
                    if (FormationLayout.Find(ref library, FormationType.Dispersed) >= 0)
                        intent.desiredFormation = FormationType.Dispersed;
                }
            }
        }

        if (validFormationChange)
            intent.desiredFormation = selectedFormation;
        input.hasNewOrder |= command.orderIssued;
        return true;
    }

    public static bool TryResolveFormation(SquadDefinitionComponent definition,
        FormationType current, int requestedIndex, bool cycle, out FormationType selected)
    {
        selected = current;
        if (!definition.formationLibrary.IsCreated)
            return false;

        ref var formations = ref definition.formationLibrary.Value.formations;
        if (formations.Length == 0)
            return false;

        int index = requestedIndex;
        if (cycle)
        {
            index = -1;
            for (int i = 0; i < formations.Length; i++)
                if (formations[i].formationType == current)
                {
                    index = i;
                    break;
                }
            index = (index + 1) % formations.Length;
        }
        if (index < 0 || index >= formations.Length)
            return false;

        selected = formations[index].formationType;
        return true;
    }
}
