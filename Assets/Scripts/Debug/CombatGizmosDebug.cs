// DEBUG ONLY — safe to delete for production
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// Debug gizmos for validating the combat system (hitboxes, hurtboxes, health, targeting).
/// Attach to a GameObject named [DEBUG] Combat Gizmos in the scene.
/// All drawing code is inside #if UNITY_EDITOR — compiles to nothing in builds.
/// </summary>
public class CombatGizmosDebug : MonoBehaviour
{
#if UNITY_EDITOR
    // ── Capsule dims (must match SquadSpawning.System.cs hardcoded values) ────
    const float CapsuleBottomY = 0.4f;
    const float CapsuleTopY    = 1.6f;
    const float CapsuleRadius  = 0.35f;

    // ── Colors ────────────────────────────────────────────────────────────────
    static readonly Color HitboxActiveColor         = Color.red;
    static readonly Color HitboxInactiveColor       = new Color(1f, 1f, 0f, 0.4f);
    static readonly Color DetectionCombatColor      = Color.red;
    static readonly Color AttackRangeNoTargetColor  = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color AttackRangeInRangeColor   = Color.red;
    static readonly Color AttackRangeFarColor       = Color.yellow;
    static readonly Color TargetLineColor           = Color.magenta;

    void OnDrawGizmos()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        var em = world.EntityManager;

        DrawUnitGizmos(em);
        DrawSquadDetectionRanges(em);
    }

    // ── Unit gizmos (hurtbox, hitbox, health bar, target line) ────────────────

    void DrawUnitGizmos(EntityManager em)
    {
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<HealthComponent>(),
            ComponentType.ReadOnly<UnitWeaponComponent>(),
            ComponentType.ReadOnly<UnitCombatComponent>());

        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            var lt     = em.GetComponentData<LocalTransform>(entity);
            var health = em.GetComponentData<HealthComponent>(entity);
            var weapon = em.GetComponentData<UnitWeaponComponent>(entity);
            var combat = em.GetComponentData<UnitCombatComponent>(entity);

            Vector3 pos = lt.Position;
            Vector3 fwd = math.mul(lt.Rotation, math.forward());

            var team = em.HasComponent<TeamComponent>(entity)
                     ? em.GetComponentData<TeamComponent>(entity).value : Team.None;
            DrawHurtbox(pos, GetTeamHurtboxColor(team));
            DrawWeaponHitbox(pos, fwd, weapon, entity, em);
            DrawAttackRangeCircle(pos, weapon.attackRange, combat, em);
            DrawHealthBar(pos, health);
            DrawTargetLine(pos, combat, em);
        }
        entities.Dispose();
        query.Dispose();
    }

    // ── Hurtbox capsule approximation ─────────────────────────────────────────

    void DrawHurtbox(Vector3 pos, Color color)
    {
        Gizmos.color = color;
        Vector3 bottom = pos + Vector3.up * CapsuleBottomY;
        Vector3 top    = pos + Vector3.up * CapsuleTopY;
        Gizmos.DrawWireSphere(bottom, CapsuleRadius);
        Gizmos.DrawWireSphere(top,    CapsuleRadius);
        Gizmos.DrawLine(bottom + Vector3.right   * CapsuleRadius, top + Vector3.right   * CapsuleRadius);
        Gizmos.DrawLine(bottom - Vector3.right   * CapsuleRadius, top - Vector3.right   * CapsuleRadius);
        Gizmos.DrawLine(bottom + Vector3.forward * CapsuleRadius, top + Vector3.forward * CapsuleRadius);
        Gizmos.DrawLine(bottom - Vector3.forward * CapsuleRadius, top - Vector3.forward * CapsuleRadius);
    }

    // ── Weapon hitbox arc (attack range forward indicator) ───────────────────

    void DrawWeaponHitbox(Vector3 pos, Vector3 fwd, UnitWeaponComponent weapon, Entity entity, EntityManager em)
    {
        bool active = em.HasComponent<WeaponHitboxActiveTag>(entity)
                   && em.IsComponentEnabled<WeaponHitboxActiveTag>(entity);

        // Draw a small forward line + arc to indicate the attack range direction
        Gizmos.color = active ? HitboxActiveColor : HitboxInactiveColor;
        Gizmos.DrawLine(pos + Vector3.up * 1f, pos + Vector3.up * 1f + fwd * weapon.attackRange);
    }

    // ── Health bar (world-space lines above the unit) ─────────────────────────

    void DrawHealthBar(Vector3 pos, HealthComponent health)
    {
        if (health.maxHealth <= 0f) return;

        float ratio    = Mathf.Clamp01(health.currentHealth / health.maxHealth);
        float barWidth = 1f;
        float barY     = 2.2f;
        Vector3 left   = pos + Vector3.up * barY - Vector3.right * (barWidth * 0.5f);
        Vector3 right  = left + Vector3.right * barWidth;
        Vector3 filled = left + Vector3.right * (barWidth * ratio);

        Gizmos.color = Color.gray;
        Gizmos.DrawLine(left, right);

        Gizmos.color = ratio > 0.5f ? Color.green : ratio > 0.25f ? Color.yellow : Color.red;
        if (ratio > 0f) Gizmos.DrawLine(left, filled);
    }

    // ── Target line ───────────────────────────────────────────────────────────

    void DrawTargetLine(Vector3 pos, UnitCombatComponent combat, EntityManager em)
    {
        if (combat.target == Entity.Null || !em.Exists(combat.target)) return;
        if (!em.HasComponent<LocalTransform>(combat.target)) return;

        var targetLt = em.GetComponentData<LocalTransform>(combat.target);
        Gizmos.color = TargetLineColor;
        Gizmos.DrawLine(pos + Vector3.up, (Vector3)targetLt.Position + Vector3.up);
    }

    // ── Attack range circle per unit ─────────────────────────────────────────

    void DrawAttackRangeCircle(Vector3 pos, float attackRange, UnitCombatComponent combat, EntityManager em)
    {
        Color ringColor;
        if (combat.target == Entity.Null || !em.Exists(combat.target))
        {
            ringColor = AttackRangeNoTargetColor;
        }
        else
        {
            var targetLt = em.GetComponentData<LocalTransform>(combat.target);
            float dist   = math.distance((float3)pos, targetLt.Position);
            ringColor    = dist <= attackRange ? AttackRangeInRangeColor : AttackRangeFarColor;
        }
        DrawWireCircle(pos, attackRange, ringColor);
    }

    // ── Squad detection range circles ─────────────────────────────────────────

    void DrawSquadDetectionRanges(EntityManager em)
    {
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<SquadDefinitionComponent>(),
            ComponentType.ReadOnly<SquadStateComponent>());

        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            // Compute centroid from alive unit positions — same logic as EnemyDetectionSystem
            float3 centroid   = float3.zero;
            int    aliveCount = 0;
            if (em.HasBuffer<SquadUnitElement>(entity))
            {
                var units = em.GetBuffer<SquadUnitElement>(entity, true);
                for (int i = 0; i < units.Length; i++)
                {
                    Entity u = units[i].Value;
                    if (!em.Exists(u) || !em.HasComponent<LocalTransform>(u)) continue;
                    centroid += em.GetComponentData<LocalTransform>(u).Position;
                    aliveCount++;
                }
            }
            if (aliveCount == 0) continue;
            centroid /= aliveCount;

            var data  = em.GetComponentData<SquadDefinitionComponent>(entity);
            var aiComp = em.GetComponentData<SquadAIComponent>(entity);
            bool hasTargets = em.HasBuffer<SquadTargetEntity>(entity)
                           && em.GetBuffer<SquadTargetEntity>(entity).Length > 0;
            var team = em.HasComponent<TeamComponent>(entity)
                     ? em.GetComponentData<TeamComponent>(entity).value : Team.None;

            Color ringColor = GetDetectionColor(team, aiComp.isInCombat, hasTargets);
            DrawWireCircle(centroid + new float3(0f, 0.05f, 0f), data.detectionRange, ringColor);

            string label = $"{team} | {(aiComp.isInCombat ? "COMBAT" : hasTargets ? "targets" : "idle")}";
            UnityEditor.Handles.color = ringColor;
            UnityEditor.Handles.Label((Vector3)centroid + Vector3.up * 2.5f, label);
        }
        entities.Dispose();
        query.Dispose();
    }

    static Color GetTeamHurtboxColor(Team team) => team switch {
        Team.TeamA => Color.cyan,
        Team.TeamB => new Color(1f, 0.55f, 0f),
        _          => Color.gray
    };

    static Color GetDetectionColor(Team team, bool inCombat, bool hasTargets)
    {
        return team switch {
            Team.TeamA => inCombat  ? Color.red
                        : hasTargets ? new Color(1f, 0.5f, 0f)
                        : Color.green,
            Team.TeamB => inCombat  ? Color.magenta
                        : hasTargets ? Color.yellow
                        : new Color(0f, 0.7f, 0.7f),
            _          => inCombat  ? Color.red
                        : hasTargets ? new Color(1f, 0.5f, 0f)
                        : Color.green,
        };
    }

    void DrawWireCircle(Vector3 center, float radius, Color color, int segments = 32)
    {
        Gizmos.color = color;
        float step = 2f * Mathf.PI / segments;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i       * step;
            float a1 = (i + 1) * step;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            Gizmos.DrawLine(p0, p1);
        }
    }

#endif // UNITY_EDITOR
}
