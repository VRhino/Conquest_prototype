// DEBUG ONLY — safe to delete for production
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Debug gizmos: draws a state label above each unit showing SquadFSMState + UnitFormationState.
/// Attach to a GameObject named [DEBUG] Combat Gizmos in the scene.
/// All drawing code is inside #if UNITY_EDITOR — compiles to nothing in builds.
/// </summary>
public class CombatGizmosDebug : MonoBehaviour
{
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        DrawUnitStateLabels(world.EntityManager);
    }

    void DrawUnitStateLabels(EntityManager em)
    {
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<SquadStateComponent>(),
            ComponentType.ReadOnly<SquadAIComponent>());

        var squads = query.ToEntityArray(Allocator.Temp);
        foreach (var squad in squads)
        {
            if (!em.HasBuffer<SquadUnitElement>(squad)) continue;

            var units      = em.GetBuffer<SquadUnitElement>(squad, true);
            var squadState = em.GetComponentData<SquadStateComponent>(squad);

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!em.Exists(unit) || !em.HasComponent<LocalTransform>(unit)) continue;

                var lt = em.GetComponentData<LocalTransform>(unit);

                string unitFormState = em.HasComponent<UnitFormationStateComponent>(unit)
                    ? em.GetComponentData<UnitFormationStateComponent>(unit).State.ToString()
                    : "?";

                string label = $"{squadState.currentState}\n{unitFormState}";

                UnityEditor.Handles.color = GetStateColor(squadState.currentState);
                UnityEditor.Handles.Label((Vector3)lt.Position + Vector3.up * 2.4f, label);
            }
        }
        squads.Dispose();
        query.Dispose();
    }

    static Color GetStateColor(SquadFSMState state) => state switch
    {
        SquadFSMState.InCombat        => Color.red,
        SquadFSMState.FollowingHero   => Color.green,
        SquadFSMState.HoldingPosition => Color.cyan,
        SquadFSMState.Retreating      => Color.magenta,
        SquadFSMState.KO              => Color.gray,
        _                             => Color.white
    };
#endif
}
