using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Validates that all requirements for ranged units (archers, crossbowmen, etc.) are met.
/// Covers asset configuration, scene setup, and — when in Play Mode — live ECS entity state.
///
/// Run: Tools > Battle > Ranged Units Diagnostic
/// </summary>
public static class RangedUnitsDiagnostic
{
    [MenuItem("Tools/Battle/Ranged Units Diagnostic")]
    public static void RunDiagnostic()
    {
        int errors   = 0;
        int warnings = 0;

        Log("══════════════════════════════════════════");
        Log("         RANGED UNITS DIAGNOSTIC          ");
        Log("══════════════════════════════════════════");

        // ── 1. Asset-level checks ─────────────────────────────────────────
        var rangedSquads = FindRangedSquadData();

        if (rangedSquads.Count == 0)
        {
            Warn("No SquadData assets found with rangedData assigned. Nothing to validate.", ref warnings);
        }
        else
        {
            Log($"[ASSETS] Found {rangedSquads.Count} ranged squad(s):");
            foreach (var sd in rangedSquads)
                errors += ValidateSquadDataAsset(sd);
        }

        // ── 2. Scene setup checks ─────────────────────────────────────────
        errors += CheckRangedCombatBootstrap(rangedSquads, ref warnings);

        // ── 3. Runtime ECS checks (Play Mode only) ────────────────────────
        if (Application.isPlaying)
        {
            errors += CheckECSEntities(rangedSquads, ref warnings);
            errors += CheckObjectPools(rangedSquads, ref warnings);
        }
        else
        {
            Log("[INFO] Enter Play Mode to run ECS entity and pool checks.");
        }

        // ── Summary ───────────────────────────────────────────────────────
        Log("══════════════════════════════════════════");
        if (errors == 0 && warnings == 0)
            Debug.Log("[RangedDiagnostic] ✓ ALL CHECKS PASSED — ranged units should fire correctly.");
        else
            Debug.LogError($"[RangedDiagnostic] RESULT: {errors} error(s), {warnings} warning(s). See console for details.");
    }

    // ──────────────────────────────────────────────────────────────────────
    // 1. Asset validation
    // ──────────────────────────────────────────────────────────────────────

    static List<SquadData> FindRangedSquadData()
    {
        return AssetDatabase
            .FindAssets("t:SquadData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<SquadData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(sd => sd != null && sd.IsRanged)
            .ToList();
    }

    static int ValidateSquadDataAsset(SquadData sd)
    {
        int errors = 0;
        string label = $"  [{sd.squadName ?? sd.name}]";
        var r = sd.rangedData;

        if (r == null)
        {
            Error($"{label} rangedData is null — assign a SquadRangedData asset to enable ranged behavior.", ref errors);
            return errors;
        }

        // Pool key
        if (string.IsNullOrEmpty(r.projectilePoolKey))
            Error($"{label} projectilePoolKey is empty — ProjectileSpawnSystem cannot retrieve the prefab from the pool.", ref errors);
        else
            OK($"{label} projectilePoolKey = \"{r.projectilePoolKey}\"");

        // Projectile prefab
        if (r.projectilePrefab == null)
            Error($"{label} projectilePrefab is null — RangedCombatBootstrap cannot warm the pool.", ref errors);
        else
            OK($"{label} projectilePrefab = {r.projectilePrefab.name}");

        // Attack range
        if (r.range <= 0f)
            Error($"{label} range = {r.range} — must be > 0 or units will never pass the range check.", ref errors);
        else
            OK($"{label} range = {r.range}");

        // Detection range vs attack range (detectionRange stays on base SquadData)
        if (sd.detectionRange < r.range)
            Error($"{label} detectionRange ({sd.detectionRange}) < range ({r.range}) — enemies can enter attack range without triggering InCombat state; archers will never target them.", ref errors);
        else
            OK($"{label} detectionRange ({sd.detectionRange}) >= range ({r.range})");

        // Fire rate
        if (r.fireRate <= 0f)
            Error($"{label} fireRate = {r.fireRate} — shot cooldown will be 1 / fireRate; a zero value causes division-by-zero fallback (1 s).", ref errors);
        else
            OK($"{label} fireRate = {r.fireRate}");

        // Ammo
        if (r.ammo <= 0)
            Error($"{label} ammo = {r.ammo} — units start with no ammo and immediately enter reload loop.", ref errors);
        else
            OK($"{label} ammo = {r.ammo}");

        // Accuracy is authored as 1..100.
        if (r.accuracy < 1f || r.accuracy > 100f)
            Debug.LogWarning($"[RangedDiagnostic] {label} accuracy = {r.accuracy} — expected range is 1..100.");

        return errors;
    }

    // ──────────────────────────────────────────────────────────────────────
    // 2. Scene: RangedCombatBootstrap
    // ──────────────────────────────────────────────────────────────────────

    static int CheckRangedCombatBootstrap(List<SquadData> rangedSquads, ref int warnings)
    {
        int errors = 0;

        var bootstrap = Object.FindFirstObjectByType<RangedCombatBootstrap>();
        if (bootstrap == null)
        {
            if (rangedSquads.Count > 0)
                Error($"[SCENE] RangedCombatBootstrap not found in the active scene — the ObjectPool will not be pre-warmed and projectiles cannot spawn.", ref errors);
            else
                Log("[SCENE] RangedCombatBootstrap not found (no ranged squads configured, may be intentional).");
            return errors;
        }

        OK($"[SCENE] RangedCombatBootstrap found on '{bootstrap.gameObject.name}'.");

        // Inspect its [SerializeField] rangedSquads array via SerializedObject
        var so = new SerializedObject(bootstrap);
        var sqProp = so.FindProperty("rangedSquads");

        if (sqProp == null || sqProp.arraySize == 0)
        {
            Warn("[SCENE] RangedCombatBootstrap.rangedSquads array is empty — pools will not be warmed.", ref warnings);
            return errors;
        }

        var bootstrapSquads = new HashSet<SquadData>();
        for (int i = 0; i < sqProp.arraySize; i++)
        {
            var entry = sqProp.GetArrayElementAtIndex(i).objectReferenceValue as SquadData;
            if (entry != null)
                bootstrapSquads.Add(entry);
        }

        OK($"[SCENE] RangedCombatBootstrap has {bootstrapSquads.Count} squad(s) assigned.");

        foreach (var sd in rangedSquads)
        {
            if (!bootstrapSquads.Contains(sd))
                Warn($"[SCENE] '{sd.squadName ?? sd.name}' is a ranged squad but is NOT in RangedCombatBootstrap.rangedSquads — its pool will not be warmed.", ref warnings);
            else
                OK($"[SCENE] '{sd.squadName ?? sd.name}' is registered in RangedCombatBootstrap.");
        }

        return errors;
    }

    // ──────────────────────────────────────────────────────────────────────
    // 3. Runtime: ECS entity state
    // ──────────────────────────────────────────────────────────────────────

    static int CheckECSEntities(List<SquadData> rangedSquads, ref int warnings)
    {
        int errors = 0;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Error("[ECS] DefaultGameObjectInjectionWorld is null — ECS world not running.", ref errors);
            return errors;
        }

        var em = world.EntityManager;

        // Count units with UnitRangedStatsComponent
        var rangedStatsQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<UnitRangedStatsComponent>());
        int rangedStatsCount = rangedStatsQuery.CalculateEntityCount();
        rangedStatsQuery.Dispose();

        if (rangedStatsCount == 0)
            Error("[ECS] No entities found with UnitRangedStatsComponent — UnitStatsUtility.ApplyStatsToUnit has not run for ranged units yet. Check that UnitStatScalingSystem is active and that squad entities have SquadProgressComponent.", ref errors);
        else
            OK($"[ECS] {rangedStatsCount} unit(s) have UnitRangedStatsComponent.");

        // Count units with RangedAttackStateComponent
        var rangedStateQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<RangedAttackStateComponent>());
        int rangedStateCount = rangedStateQuery.CalculateEntityCount();
        rangedStateQuery.Dispose();

        if (rangedStateCount == 0)
            Error("[ECS] No entities found with RangedAttackStateComponent — RangedAttackSystem cannot process any unit.", ref errors);
        else
            OK($"[ECS] {rangedStateCount} unit(s) have RangedAttackStateComponent.");

        // Mismatch between the two
        if (rangedStatsCount > 0 && rangedStateCount > 0 && rangedStatsCount != rangedStateCount)
            Warn($"[ECS] UnitRangedStatsComponent count ({rangedStatsCount}) != RangedAttackStateComponent count ({rangedStateCount}) — some units are partially initialized.", ref warnings);

        // Check squads have SquadProgressComponent (required by UnitStatScalingSystem)
        var squadProgressQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<SquadProgressComponent>(),
            ComponentType.ReadOnly<SquadDataComponent>());
        int progressCount = squadProgressQuery.CalculateEntityCount();
        squadProgressQuery.Dispose();

        var squadDataQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<SquadDataComponent>());
        int squadCount = squadDataQuery.CalculateEntityCount();
        squadDataQuery.Dispose();

        if (squadCount > 0 && progressCount == 0)
            Error($"[ECS] {squadCount} squad entity(ies) found but none have SquadProgressComponent — UnitStatScalingSystem will not run, ranged stats will never be applied.", ref errors);
        else if (progressCount > 0)
            OK($"[ECS] {progressCount}/{squadCount} squad(s) have SquadProgressComponent (required by UnitStatScalingSystem).");

        // Check UnitDetectedEnemy buffer exists on ranged units
        var detectedBufQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<UnitRangedStatsComponent>(),
            ComponentType.ReadOnly<UnitDetectedEnemy>());
        int withBufferCount = detectedBufQuery.CalculateEntityCount();
        detectedBufQuery.Dispose();

        if (rangedStatsCount > 0 && withBufferCount == 0)
            Warn($"[ECS] Ranged units exist but none have a UnitDetectedEnemy buffer — EnemyDetectionSystem may not have propagated detections yet, or units were spawned without the buffer.", ref warnings);
        else if (withBufferCount > 0)
            OK($"[ECS] {withBufferCount} ranged unit(s) have UnitDetectedEnemy buffer.");

        return errors;
    }

    // ──────────────────────────────────────────────────────────────────────
    // 4. Runtime: ObjectPoolSystem
    // ──────────────────────────────────────────────────────────────────────

    static int CheckObjectPools(List<SquadData> rangedSquads, ref int warnings)
    {
        int errors = 0;

        var poolSystem = Object.FindFirstObjectByType<ObjectPoolSystem>();
        if (poolSystem == null)
        {
            Error("[POOL] ObjectPoolSystem not found in scene — projectiles cannot be spawned.", ref errors);
            return errors;
        }

        OK("[POOL] ObjectPoolSystem instance found.");

        // Use reflection to read the internal pools dictionary (key: string)
        var poolsField = typeof(ObjectPoolSystem).GetField("pools",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (poolsField == null)
        {
            // Field name may differ; try "poolDictionary" as fallback
            poolsField = typeof(ObjectPoolSystem).GetField("poolDictionary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        if (poolsField == null)
        {
            Warn("[POOL] Cannot inspect ObjectPoolSystem internal pool dictionary (field not found via reflection). Skipping pool key checks.", ref warnings);
            return errors;
        }

        var poolsObj = poolsField.GetValue(poolSystem);
        if (poolsObj == null)
        {
            Warn("[POOL] ObjectPoolSystem pool dictionary is null — WarmUpPool may not have been called yet.", ref warnings);
            return errors;
        }

        // The dictionary is expected to be Dictionary<string, ...>
        var poolsDict = poolsObj as System.Collections.IDictionary;
        if (poolsDict == null)
        {
            Warn("[POOL] ObjectPoolSystem pool dictionary is not an IDictionary — cannot inspect keys.", ref warnings);
            return errors;
        }

        foreach (var sd in rangedSquads)
        {
            var poolKey = sd.rangedData?.projectilePoolKey;
            if (string.IsNullOrEmpty(poolKey))
                continue; // Already caught in asset validation

            if (!poolsDict.Contains(poolKey))
                Error($"[POOL] Pool key \"{poolKey}\" ({sd.squadName ?? sd.name}) is NOT registered in ObjectPoolSystem — RangedCombatBootstrap.Start() may not have run yet, or the squad is missing from its list.", ref errors);
            else
                OK($"[POOL] Pool \"{poolKey}\" ({sd.squadName ?? sd.name}) is registered.");
        }

        return errors;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Logging helpers
    // ──────────────────────────────────────────────────────────────────────

    static void OK(string msg)    => Debug.Log($"[RangedDiagnostic] ✓ {msg}");
    static void Warn(string msg, ref int counter)  { counter++; Debug.LogWarning($"[RangedDiagnostic] ⚠ {msg}"); }
    static void Error(string msg, ref int counter) { counter++; Debug.LogError($"[RangedDiagnostic] ✗ {msg}"); }
    static void Log(string msg)   => Debug.Log($"[RangedDiagnostic] {msg}");
}
