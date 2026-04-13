using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using ConquestTactics.Visual;
using System.Collections.Generic;

/// <summary>
/// MonoBehaviour on pooled projectile GameObjects (arrows, bolts, etc.).
///
/// Supports two trajectory modes set at fire time via ProjectileTrajectory:
///
///   Arc (archers):
///     position(t) = lerp(startPos, targetPos, t) + up * arcHeight * sin(π * t)
///     tangent(t)  = (targetPos - startPos) + up * arcHeight * π * cos(π * t)
///     Arrow points upward at launch, tilts down as it descends.
///     Arc height scales with horizontal distance (clamped).
///
///   Straight (crossbowmen):
///     Constant-speed movement directly toward the fixed target snapshot.
///     Always points at the destination.
///
/// Target lock behavior:
///   Destination is captured once at fire time and never updated in flight.
///   If the target moves away, projectile can miss (no fallback auto-hit).
/// </summary>
public class ProjectileController : MonoBehaviour
{
    [Header("Shared")]
    [SerializeField] float impactDistance  = 0.6f;
    [SerializeField] float armDelaySeconds = 0.05f;

    [Header("Arc (Archers)")]
    [SerializeField] float arcSpeed        = 22f;
    [SerializeField] float arcHeightFactor = 0.28f;
    [SerializeField] float minArcHeight    = 0.4f;
    [SerializeField] float maxArcHeight    = 6f;

    [Header("Straight (Crossbowmen)")]
    [SerializeField] float straightSpeed   = 35f;

    // --- shared state ---
    Entity  _shooter;
    Entity  _target;
    Entity  _damageProfile;
    Team    _sourceTeam;
    float3  _spawnPosition;
    float   _multiplier;
    string  _poolKey;

    float3               _impactPoint;
    ProjectileTrajectory _trajectory;
    bool                _impacted;
    float               _spawnTime;

    readonly List<ColliderPair> _ignoredSpawnPairs = new();

    // --- arc-only state ---
    Vector3 _startPos;
    float   _flightDuration;
    float   _arcHeight;
    float   _elapsed;

    // -----------------------------------------------------------------------

    struct ColliderPair
    {
        public Collider projectileCollider;
        public Collider shooterCollider;
    }

    /// <summary>Called by ProjectileSpawnSystem immediately after pool retrieval.</summary>
    public void Initialize(
        Entity              shooter,
        Entity              target,
        float3              spawnPos,
        float3              attackDir,
        Entity              damageProfile,
        Team                sourceTeam,
        float               multiplier,
        string              poolKey,
        ProjectileTrajectory trajectory)
    {
        _shooter         = shooter;
        _target          = target;
        _damageProfile   = damageProfile;
        _sourceTeam      = sourceTeam;
        _spawnPosition   = spawnPos;
        _multiplier      = multiplier;
        _poolKey         = poolKey;
        _trajectory      = trajectory;
        _impacted        = false;
        _elapsed         = 0f;
        _spawnTime       = Time.time;
        _startPos        = new Vector3(spawnPos.x, spawnPos.y, spawnPos.z);

        // Capture a fixed destination at shot time (non-homing projectile).
        _impactPoint = CaptureImpactPoint(spawnPos, attackDir, target);

        transform.position = _startPos;

        if (trajectory == ProjectileTrajectory.Arc)
            InitArc();
        else
            InitStraight();

        IgnoreShooterCollisions();
    }

    // -----------------------------------------------------------------------

    void InitArc()
    {
        Vector3 dest      = V3(_impactPoint);
        float   horizDist = Vector2.Distance(
            new Vector2(_startPos.x, _startPos.z),
            new Vector2(dest.x, dest.z));

        _arcHeight      = Mathf.Clamp(horizDist * arcHeightFactor, minArcHeight, maxArcHeight);
        _flightDuration = Mathf.Max(horizDist / arcSpeed, 0.1f);

        // Orient along initial tangent (points upward-forward)
        Vector3 tangent = (dest - _startPos) + Vector3.up * (_arcHeight * Mathf.PI);
        if (tangent.sqrMagnitude > 0.001f)
            transform.forward = tangent.normalized;
    }

    void InitStraight()
    {
        Vector3 dest = V3(_impactPoint);
        Vector3 dir  = dest - _startPos;
        if (dir.sqrMagnitude > 0.001f)
            transform.forward = dir.normalized;
    }

    // -----------------------------------------------------------------------

    void Update()
    {
        if (_impacted) return;

        if (_trajectory == ProjectileTrajectory.Arc)
            MoveArc();
        else
            MoveStraight();
    }

    void MoveArc()
    {
        _elapsed += Time.deltaTime;
        float t    = Mathf.Clamp01(_elapsed / _flightDuration);
        Vector3 dest = V3(_impactPoint);

        // Parabolic position
        Vector3 flat = Vector3.Lerp(_startPos, dest, t);
        float   arcY = _arcHeight * Mathf.Sin(Mathf.PI * t);
        transform.position = flat + Vector3.up * arcY;

        // Orient along tangent of arc
        Vector3 tangent = (dest - _startPos) + Vector3.up * (_arcHeight * Mathf.PI * Mathf.Cos(Mathf.PI * t));
        if (tangent.sqrMagnitude > 0.001f)
            transform.forward = tangent.normalized;

        bool arrived     = t >= 1f;
        bool closeEnough = Vector3.Distance(transform.position, dest) < impactDistance;

        if (arrived || closeEnough)
            TriggerImpact(HitType.Body, Entity.Null);
    }

    void MoveStraight()
    {
        Vector3 dest  = V3(_impactPoint);
        Vector3 delta = dest - transform.position;
        float   dist  = delta.magnitude;

        if (dist > 0.01f)
        {
            transform.forward  = delta.normalized;
            transform.position += delta.normalized * (straightSpeed * Time.deltaTime);
        }

        if (dist < impactDistance)
            TriggerImpact(HitType.Body, Entity.Null);
    }

    // -----------------------------------------------------------------------

    float3 CaptureImpactPoint(float3 spawnPos, float3 attackDir, Entity target)
    {
        float3 fallback = spawnPos + attackDir * 20f;
        if (target == Entity.Null) return fallback;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return fallback;

        var em = world.EntityManager;
        if (!em.Exists(target) || !em.HasComponent<LocalTransform>(target))
            return fallback;

        var lt = em.GetComponentData<LocalTransform>(target);
        return lt.Position + new float3(0f, 0.9f, 0f);
    }

    /// <summary>
    /// Physics collision detection: resolves the actual entity touched (shield owner or body).
    /// Damage goes to whoever was physically hit, not necessarily the original _target.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (_impacted) return;
        if (Time.time - _spawnTime < armDelaySeconds) return;

        Entity  actualTarget;
        HitType hitType;

        var shield = other.GetComponent<ShieldHitboxBehaviour>();
        if (shield != null)
        {
            if (shield.ownerUnit == Entity.Null) return;
            actualTarget = shield.ownerUnit;
            hitType      = HitType.Shield;
        }
        else
        {
            var sync = other.GetComponentInParent<EntityVisualSync>();
            if (sync == null) return;  // Terrain, obstacles, etc.
            actualTarget = sync.GetHeroEntity();
            if (actualTarget == Entity.Null) return;
            hitType = HitType.Body;
        }

        // Never resolve impact against the shooter itself.
        if (actualTarget == _shooter)
            return;

        // Ignore allies at collision time so arrows do not detonate on friendly units.
        if (IsSameTeam(actualTarget))
            return;

        TriggerImpact(hitType, actualTarget);
    }

    // actualTarget: entity physically hit; falls back to _target when called from distance check.
    void TriggerImpact(HitType hitType = HitType.Body, Entity actualTarget = default)
    {
        _impacted = true;
        RestoreIgnoredSpawnCollisions();

        Entity damageTarget = actualTarget;

        if (damageTarget != Entity.Null && _damageProfile != Entity.Null)
        {
            ProjectileImpactQueue.Pending.Enqueue(new ProjectileImpactData
            {
                shooter          = _shooter,
                target           = damageTarget,
                damageProfile    = _damageProfile,
                sourceTeam       = _sourceTeam,
                attackerPosition = _spawnPosition,
                multiplier       = _multiplier,
                hitType          = hitType
            });
        }

        if (ObjectPoolSystem.Instance != null)
            ObjectPoolSystem.Instance.ReturnToPool(_poolKey, gameObject);
        else
            gameObject.SetActive(false);
    }

    bool IsSameTeam(Entity other)
    {
        if (_sourceTeam == Team.None || other == Entity.Null) return false;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        var em = world.EntityManager;
        if (!em.Exists(other) || !em.HasComponent<TeamComponent>(other)) return false;

        return em.GetComponentData<TeamComponent>(other).value == _sourceTeam;
    }

    void IgnoreShooterCollisions()
    {
        RestoreIgnoredSpawnCollisions();

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated || _shooter == Entity.Null)
            return;

        var projectileColliders = GetComponentsInChildren<Collider>(true);
        if (projectileColliders == null || projectileColliders.Length == 0)
            return;

        var allSyncs = FindObjectsByType<EntityVisualSync>(FindObjectsSortMode.None);
        foreach (var sync in allSyncs)
        {
            if (sync == null || sync.GetHeroEntity() != _shooter) continue;

            var shooterColliders = sync.GetComponentsInChildren<Collider>(true);
            foreach (var pCol in projectileColliders)
            {
                if (pCol == null) continue;
                foreach (var sCol in shooterColliders)
                {
                    if (sCol == null) continue;
                    Physics.IgnoreCollision(pCol, sCol, true);
                    _ignoredSpawnPairs.Add(new ColliderPair
                    {
                        projectileCollider = pCol,
                        shooterCollider = sCol
                    });
                }
            }
            break;
        }
    }

    void RestoreIgnoredSpawnCollisions()
    {
        if (_ignoredSpawnPairs.Count == 0) return;

        for (int i = 0; i < _ignoredSpawnPairs.Count; i++)
        {
            var pair = _ignoredSpawnPairs[i];
            if (pair.projectileCollider != null && pair.shooterCollider != null)
                Physics.IgnoreCollision(pair.projectileCollider, pair.shooterCollider, false);
        }

        _ignoredSpawnPairs.Clear();
    }

    static Vector3 V3(float3 v) => new Vector3(v.x, v.y, v.z);

    void OnDisable()
    {
        RestoreIgnoredSpawnCollisions();
    }
}
