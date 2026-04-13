// DEBUG ONLY — safe to delete for production
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Debug gizmos: draws a state label above each unit showing SquadFSMState + UnitFormationState.
/// Optionally draws lines to assigned targets and detected enemies for combat debugging.
/// Attach to a GameObject named [DEBUG] Combat Gizmos in the scene.
/// All drawing code is inside #if UNITY_EDITOR — compiles to nothing in builds.
/// </summary>
public class CombatGizmosDebug : MonoBehaviour
{
#if UNITY_EDITOR
    public bool showTargetLines    = true;   // Red line from unit to assigned target
    public bool showDetectedLines  = false;  // Gray lines to all detected enemies
    public bool showNoTargetMarker = true;   // Magenta sphere on InCombat unit with no target

    void OnDrawGizmos()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        var em = world.EntityManager;
        DrawUnitStateLabels(em);
        DrawCombatTargetLines(em);
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

                // Ranged range circle
                if (em.HasComponent<UnitRangedStatsComponent>(unit))
                {
                    float range = em.GetComponentData<UnitRangedStatsComponent>(unit).range;
                    UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.35f); // orange, semi-transparent
                    UnityEditor.Handles.DrawWireDisc((Vector3)lt.Position, Vector3.up, range);
                }
            }
        }
        squads.Dispose();
        query.Dispose();
    }

    void DrawCombatTargetLines(EntityManager em)
    {
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<SquadStateComponent>(),
            ComponentType.ReadOnly<SquadAIComponent>());

        var squads = query.ToEntityArray(Allocator.Temp);
        foreach (var squad in squads)
        {
            if (!em.HasBuffer<SquadUnitElement>(squad)) continue;

            var units      = em.GetBuffer<SquadUnitElement>(squad, true);
            var squadState = em.HasComponent<SquadStateComponent>(squad)
                ? em.GetComponentData<SquadStateComponent>(squad)
                : default;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!em.Exists(unit) || !em.HasComponent<LocalTransform>(unit)) continue;

                var lt = em.GetComponentData<LocalTransform>(unit);

                // Draw target assignment line (red line to assigned target)
                if (showTargetLines && em.HasComponent<UnitCombatComponent>(unit))
                {
                    var combat = em.GetComponentData<UnitCombatComponent>(unit);
                    if (combat.target != Entity.Null && em.Exists(combat.target)
                        && em.HasComponent<LocalTransform>(combat.target))
                    {
                        var targetPos = em.GetComponentData<LocalTransform>(combat.target).Position;
                        UnityEditor.Handles.color = Color.red;
                        UnityEditor.Handles.DrawLine((Vector3)lt.Position, (Vector3)targetPos);
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere((Vector3)targetPos + Vector3.up * 0.3f, 0.15f);
                    }
                    else if (showNoTargetMarker
                             && squadState.currentState == SquadFSMState.InCombat
                             && combat.target == Entity.Null)
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawSphere((Vector3)lt.Position + Vector3.up * 1.8f, 0.2f);
                    }
                }

                // Draw detected enemies (gray lines to all detected enemies in buffer)
                if (showDetectedLines && em.HasBuffer<UnitDetectedEnemy>(unit))
                {
                    var detected = em.GetBuffer<UnitDetectedEnemy>(unit, true);
                    UnityEditor.Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.25f);
                    foreach (var d in detected)
                    {
                        if (d.Value != Entity.Null && em.Exists(d.Value)
                            && em.HasComponent<LocalTransform>(d.Value))
                        {
                            var ep = em.GetComponentData<LocalTransform>(d.Value).Position;
                            UnityEditor.Handles.DrawLine((Vector3)lt.Position, (Vector3)ep);
                        }
                    }
                }
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
