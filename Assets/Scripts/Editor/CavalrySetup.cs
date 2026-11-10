using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConquestTactics.Animation;
using ConquestTactics.Visual;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Authoring of the cavalry squads (M17): lance cavalry, sword cavalry and horse archers. Each soldier is one body with
/// its horse. Builds, in this order:
/// 1. The Animators: the horse's (Generic) and one per rider (Humanoid), with the same gait tree on
///    <c>MountGaitBlend</c>/<c>MountGaitScale</c> so horse and rider play in step, and the rider's attack or shot.
/// 2. The battle visuals: the horse at the root (agent, colliders, detector, adapter) and the rider of a squad on foot
///    hanging from the horse's <c>Saddle</c> bone, without its shield, with its weapon and hitbox.
/// 3. The squads: data and weapon module (initial values only: a rerun keeps manual balance), formations, ECS prefab,
///    database and visual registry.
/// Assets come from Assets/Art/Mounts/Horse (made in Blender, blender-tools/conquest-horse).
/// </summary>
public static class CavalrySetup
{
    public const string HorseFolder = "Assets/Art/Mounts/Horse";
    public const string HorseModel = HorseFolder + "/Models/Horse_Mount.fbx";
    public const string HorseController = HorseFolder + "/Animations/AC_Horse.controller";
    public const string RiderFolder = HorseFolder + "/Animations/Riders";
    const string UpperBodyMask = "Assets/Animations/CombatUpperBody.mask";
    const string VisualConfig = "Assets/ScriptableObjects/VisualPrefabConfig.asset";
    public static readonly string[] Gaits = { "Idle", "Walk", "Trot", "Gallop" };
    public const string GaitState = "Gait";
    public const string CombatLayer = "Combat";
    const string Blend = nameof(AnimationHashes.MountGaitBlend), Scale = nameof(AnimationHashes.MountGaitScale);

    /// <summary>One cavalry squad: its rider clips, the squad on foot it takes its look from, and its initial data.</summary>
    public sealed class Kind
    {
        public string key, id, displayName, rider, look, coat, dataReference, moduleReference;
        /// <summary>The look holds its bow in the right hand; the rider clips hold it in the left.</summary>
        public bool bowToLeftHand;
        public SquadType type;
        public bool neckReins, ranged;
        public Action<SquadData> data;
        public Action<ScriptableObject> module;
        public string RiderController => $"{RiderFolder}/AC_Rider_{rider}.controller";
        public string Prefab => $"Assets/Prefabs/BattleVisuals/{key}.prefab";
        public string Folder => $"Assets/Resources/Squads/{key}";
        public string VisualId => "UnitVisual_" + key;
    }

    const string SpearmenData = "Assets/Resources/Squads/Spearmen/Spearmen.asset";
    const string SpearmenMelee = "Assets/Resources/Squads/Spearmen/Spearmen_Melee.asset";
    const string ArchersData = "Assets/Resources/Squads/Levy_Archers/Levy Archers.asset";
    const string ArchersRanged = "Assets/Resources/Squads/Levy_Archers/Levy_Archer_ranged_data.asset";

    public static readonly Kind[] Kinds =
    {
        new Kind
        {
            key = "LanceCavalry", id = "lcv01", displayName = "Lance Cavalry", type = SquadType.LanceCavalry, rider = "Lancer",
            look = "Assets/Prefabs/BattleVisuals/Spearmen.prefab", coat = "Bay", dataReference = SpearmenData, moduleReference = SpearmenMelee,
            data = d => { d.baseHealth = 2000; d.baseSpeed = 5.5f; d.totalWeight = 1; d.mountAttackArc = 90; d.mountTurnRate = 120; d.baseResilience = 150; },
            module = m =>
            {
                var melee = (SquadMeleeData)m;
                melee.attackRange = 3.2f; melee.attackInterval = 2.2f; melee.attackAnimationDuration = 1f;
                melee.strikeWindowStart = 0.36f; melee.strikeWindowDuration = 0.24f; melee.kineticMultiplier = 0.8f;
                melee.hitTraits.chargeThreshold = 4f; melee.hitTraits.chargeFullDistance = 14f; melee.hitTraits.chargeMaxBonus = 1f;
            },
        },
        new Kind
        {
            key = "SwordCavalry", id = "scv01", displayName = "Sword Cavalry", type = SquadType.SwordCavalry, rider = "Sword",
            look = "Assets/Prefabs/BattleVisuals/Squires.prefab", coat = "Black", dataReference = SpearmenData, moduleReference = SpearmenMelee,
            data = d => { d.baseHealth = 2200; d.baseSpeed = 5.5f; d.totalWeight = 1; d.mountAttackArc = 220; d.mountTurnRate = 140; d.baseResilience = 170; },
            module = m =>
            {
                var melee = (SquadMeleeData)m;
                float total = melee.slashingDamage + melee.piercingDamage + melee.bluntDamage;
                melee.slashingDamage = total; melee.piercingDamage = melee.bluntDamage = 0;
                melee.slashingPenetration = Mathf.Max(melee.slashingPenetration, melee.piercingPenetration, melee.bluntPenetration);
                melee.piercingPenetration = melee.bluntPenetration = 0;
                melee.attackRange = 2.6f; melee.attackInterval = 1.6f; melee.attackAnimationDuration = 1f;
                melee.strikeWindowStart = 0.38f; melee.strikeWindowDuration = 0.22f; melee.kineticMultiplier = 0.5f;
                melee.hitTraits.chargeThreshold = 4f; melee.hitTraits.chargeFullDistance = 14f; melee.hitTraits.chargeMaxBonus = 0.3f;
            },
        },
        new Kind
        {
            key = "HorseArchers", id = "hac01", displayName = "Horse Archers", type = SquadType.HorseArchers, rider = "Archer",
            look = "Assets/Prefabs/BattleVisuals/LevyArchers.prefab", coat = "Dun", neckReins = true, ranged = true,
            bowToLeftHand = true,
            dataReference = ArchersData, moduleReference = ArchersRanged,
            data = d => { d.baseHealth = 1400; d.baseSpeed = 6f; d.totalWeight = 1; d.mountAttackArc = 300; d.mountTurnRate = 140; d.baseResilience = 90; },
            module = m => { var ranged = (SquadRangedData)m; ranged.launchHeight = 2.3f; },
        },
    };

    [MenuItem("Conquest/Cavalry/Rebuild All")]
    public static void RebuildAll()
    {
        BuildAnimators();
        BuildPrefabs();
        RegisterSquads();
    }

    // ------------------------------------------------------------------ 1. Animators

    [MenuItem("Conquest/Cavalry/1 - Rebuild Animators")]
    public static void BuildAnimators()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
        var horse = ResetController(HorseController);
        Parameter(horse, Blend, AnimatorControllerParameterType.Float, 0f);
        Parameter(horse, Scale, AnimatorControllerParameterType.Float, 1f);
        AddGaitState(horse, g => Clip($"{HorseFolder}/Animations/Horse/Horse_{g}.fbx"));
        EditorUtility.SetDirty(horse);
        foreach (var kind in Kinds) BuildRider(kind);
        AssetDatabase.SaveAssets();
        Debug.Log("Cavalry: horse and rider Animators rebuilt.");
    }

    static void BuildRider(Kind kind)
    {
        var ac = ResetController(kind.RiderController);
        Parameter(ac, Blend, AnimatorControllerParameterType.Float, 0f);
        Parameter(ac, Scale, AnimatorControllerParameterType.Float, 1f);
        Parameter(ac, nameof(AnimationHashes.IsAttacking), AnimatorControllerParameterType.Bool, 0f);
        Parameter(ac, nameof(AnimationHashes.AttackSpeed), AnimatorControllerParameterType.Float, 1f);
        if (kind.ranged) Parameter(ac, nameof(AnimationHashes.TriggerShoot), AnimatorControllerParameterType.Trigger, 0f);
        AddGaitState(ac, g => Clip($"{RiderFolder}/{kind.rider}_{g}.fbx"));

        // Attack or shot, from the waist up over the gait (the legs stay in the stirrups).
        var sm = new AnimatorStateMachine { name = CombatLayer, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ac);
        ac.AddLayer(new AnimatorControllerLayer
        {
            name = CombatLayer, stateMachine = sm, defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMask) ?? throw new FileNotFoundException(UpperBodyMask),
        });
        var rest = sm.AddState("Idle_Combat");
        rest.writeDefaultValues = true;
        sm.defaultState = rest;
        var strike = sm.AddState(kind.ranged ? "Shoot" : "Attack");
        strike.motion = Clip($"{RiderFolder}/{kind.rider}_Attack.fbx");
        strike.writeDefaultValues = false;
        strike.speedParameterActive = true;
        strike.speedParameter = nameof(AnimationHashes.AttackSpeed);
        if (kind.ranged)
        {
            // The shot is already fired when TriggerShoot arrives: the clip starts just before its release frame.
            var shoot = sm.AddAnyStateTransition(strike);
            SpearmenLineAnimationSetup.Transition(shoot, 0.08f);
            shoot.canTransitionToSelf = true;
            shoot.offset = 0.40f;
            shoot.AddCondition(AnimatorConditionMode.If, 0, nameof(AnimationHashes.TriggerShoot));
            var done = strike.AddTransition(rest);
            done.hasExitTime = true; done.exitTime = 0.92f; done.hasFixedDuration = true; done.duration = 0.15f;
        }
        else
        {
            var enter = sm.AddAnyStateTransition(strike);
            SpearmenLineAnimationSetup.Transition(enter, 0.1f);
            enter.AddCondition(AnimatorConditionMode.If, 0, nameof(AnimationHashes.IsAttacking));
            var leave = strike.AddTransition(rest);
            SpearmenLineAnimationSetup.Transition(leave, 0.15f);
            leave.AddCondition(AnimatorConditionMode.IfNot, 0, nameof(AnimationHashes.IsAttacking));
        }
        EditorUtility.SetDirty(ac);
    }

    /// <summary>The controller at <paramref name="path"/>, emptied (same asset, so references to it survive a rebuild).</summary>
    static AnimatorController ResetController(string path)
    {
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var p in ac.parameters) ac.RemoveParameter(p);
        while (ac.layers.Length > 1) ac.RemoveLayer(ac.layers.Length - 1);
        var sm = ac.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states) sm.RemoveState(s.state);
        foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().ToArray())
            UnityEngine.Object.DestroyImmediate(tree, true);
        return ac;
    }

    static void Parameter(AnimatorController ac, string name, AnimatorControllerParameterType type, float value)
        => ac.AddParameter(new AnimatorControllerParameter
        {
            name = name, type = type, defaultFloat = value, defaultBool = value != 0f, defaultInt = (int)value,
        });

    /// <summary>The gait tree: Idle, Walk, Trot and Gallop at 0..3 of MountGaitBlend, played at MountGaitScale.</summary>
    static void AddGaitState(AnimatorController ac, Func<string, AnimationClip> clip)
    {
        var tree = new BlendTree
        {
            name = GaitState, blendParameter = Blend, blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(tree, ac);
        for (int i = 0; i < Gaits.Length; i++) tree.AddChild(clip(Gaits[i]), i);
        var sm = ac.layers[0].stateMachine;
        var state = sm.AddState(GaitState);
        state.motion = tree;
        state.speedParameterActive = true;
        state.speedParameter = Scale;
        sm.defaultState = state;
    }

    public static AnimationClip Clip(string path)
        => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"))
           ?? throw new FileNotFoundException("Missing clip " + path);

    // ------------------------------------------------------------------ 2. Battle visuals

    [MenuItem("Conquest/Cavalry/2 - Build Visual Prefabs")]
    public static void BuildPrefabs()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
        foreach (var kind in Kinds) BuildPrefab(kind);
        AssetDatabase.SaveAssets();
        Debug.Log("Cavalry: battle visuals rebuilt.");
    }

    public static void BuildPrefab(Kind kind)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(HorseModel));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var look = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(kind.look));
        PrefabUtility.UnpackPrefabInstance(look, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        try
        {
            root.name = kind.key;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var horse = root.GetComponent<Animator>();
            horse.runtimeAnimatorController = Load<RuntimeAnimatorController>(HorseController);
            horse.applyRootMotion = false;
            Child(root, "Horse_Reins_Hand").SetActive(!kind.neckReins);
            Child(root, "Horse_Reins_Neck").SetActive(kind.neckReins);
            var coat = Load<Material>($"{HorseFolder}/Materials/Horse_{kind.coat}.mat");
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.sharedMaterial = coat;

            // What the soldier on foot brings: agent and adapter settings, its detector.
            var footAgent = look.GetComponent<NavMeshAgent>();
            var footAdapter = look.GetComponent<UnitAnimationAdapter>();
            var footSync = look.GetComponent<EntityVisualSync>();
            var agent = root.AddComponent<NavMeshAgent>();
            if (footAgent) EditorUtility.CopySerialized(footAgent, agent);
            agent.radius = 0.9f; agent.height = 2.6f; agent.angularSpeed = 100f; agent.acceleration = 10f;
            var adapter = root.AddComponent<UnitAnimationAdapter>();
            if (footAdapter) EditorUtility.CopySerialized(footAdapter, adapter);
            root.AddComponent<UnitAnimatorController>();
            var sync = root.AddComponent<EntityVisualSync>();
            if (footSync) EditorUtility.CopySerialized(footSync, sync);
            var detector = look.transform.Find("Detector");
            if (detector) detector.SetParent(root.transform, false);
            // Horse body along its length, rider's torso above the saddle: what enemy weapons and arrows hit.
            var body = root.AddComponent<CapsuleCollider>();
            body.direction = 2; body.center = new Vector3(0f, 1.15f, 0.2f); body.radius = 0.45f; body.height = 2.6f;
            var torso = root.AddComponent<CapsuleCollider>();
            torso.direction = 1; torso.center = new Vector3(0f, 2.15f, 0.05f); torso.radius = 0.3f; torso.height = 1.1f;

            // The rider: the soldier on foot without its gameplay components and shield, at scale 1, on the saddle.
            // Weapon moves are done first, while the look is still in its T-pose and facing +Z.
            if (kind.bowToLeftHand) BowToLeftHand(look);
            Strip(look);
            foreach (var shield in look.GetComponentsInChildren<ShieldHitboxBehaviour>(true).ToArray())
                UnityEngine.Object.DestroyImmediate(shield.gameObject);
            look.name = "Rider";
            look.transform.localScale = Vector3.one;
            var rider = look.GetComponent<Animator>();
            rider.runtimeAnimatorController = Load<RuntimeAnimatorController>(kind.RiderController);
            rider.applyRootMotion = false;
            var saddle = Child(root, "Saddle").transform;
            look.transform.SetParent(saddle, false);
            look.transform.localPosition = Vector3.zero;
            // Upright and facing the horse's front with the horse at rest: the saddle bone's own axes do not matter.
            look.transform.localRotation = Quaternion.Inverse(saddle.rotation) * root.transform.rotation;

            var mirror = root.AddComponent<RiderAnimatorMirror>();
            mirror.Bind(horse, rider);
            var serialized = new SerializedObject(mirror);
            serialized.FindProperty("_horse").objectReferenceValue = horse;
            serialized.FindProperty("_rider").objectReferenceValue = rider;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(kind.Prefab));
            PrefabUtility.SaveAsPrefabAsset(root, kind.Prefab);
        }
        finally
        {
            if (look) UnityEngine.Object.DestroyImmediate(look);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>Removes the root components of a soldier on foot except its Animator (dependants first).</summary>
    static void Strip(GameObject soldier)
    {
        foreach (var type in new[] { typeof(UnitAnimatorController), typeof(EntityVisualSync), typeof(UnitAnimationAdapter) })
            foreach (var c in soldier.GetComponents(type)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in soldier.GetComponents<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in soldier.GetComponents<NavMeshAgent>()) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in soldier.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in soldier.GetComponents<Rigidbody>()) UnityEngine.Object.DestroyImmediate(c);
    }

    /// <summary>
    /// The bow from the right fist to the left one, mirrored across the soldier's sagittal plane (T-pose, facing +Z):
    /// its limbs keep following the grip and the arrow keeps leaving towards the knuckles, as in the rider clips.
    /// </summary>
    static void BowToLeftHand(GameObject look)
    {
        var all = look.GetComponentsInChildren<Transform>(true);
        var bow = all.FirstOrDefault(t => t.name.StartsWith("SM_Prop_Bow"));
        if (!bow) throw new InvalidDataException($"{look.name} has no bow.");
        var right = all.First(t => t.name == "Hand_R");
        var left = all.First(t => t.name == "Hand_L");
        var space = look.transform;
        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        var offset = Mirror(space.InverseTransformVector(bow.position - right.position));
        var forward = Mirror(space.InverseTransformDirection(bow.forward));
        var up = Mirror(space.InverseTransformDirection(bow.up));
        bow.SetParent(left, true);
        bow.SetPositionAndRotation(left.position + space.TransformVector(offset), space.rotation * Quaternion.LookRotation(forward, up));
    }

    static GameObject Child(GameObject root, string name)
        => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)?.gameObject
           ?? throw new InvalidDataException($"{root.name} has no {name}.");

    static T Load<T>(string path) where T : UnityEngine.Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException("Missing " + path);

    // ------------------------------------------------------------------ 3. Squads

    [MenuItem("Conquest/Cavalry/3 - Register Squads")]
    public static void RegisterSquads()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
        var database = Resources.Load<SquadDatabase>("Squads/SquadDatabase") ?? throw new FileNotFoundException("SquadDatabase");
        var config = Load<VisualPrefabConfiguration>(VisualConfig);
        foreach (var kind in Kinds) Register(kind, database, config);
        foreach (var asset in new UnityEngine.Object[] { database, config })
        { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
        SquadDataService.ForceReinitialize();
        Debug.Log("Cavalry: squads registered.");
    }

    static void Register(Kind kind, SquadDatabase database, VisualPrefabConfiguration config)
    {
        if (!AssetDatabase.IsValidFolder(kind.Folder)) AssetDatabase.CreateFolder("Assets/Resources/Squads", kind.key);
        var reference = Load<SquadData>(kind.dataReference);
        string dataPath = $"{kind.Folder}/{kind.key}.asset";
        var data = AssetDatabase.LoadAssetAtPath<SquadData>(dataPath);
        // Initial values only: rerunning keeps manual balance changes.
        if (!data)
        {
            data = UnityEngine.Object.Instantiate(reference);
            data.name = kind.key;
            data.id = kind.id;
            data.squadName = kind.displayName;
            data.type = kind.type;
            data.unitType = UnitType.Cavalry;
            data.availableForRecruitmentByDefault = true;
            data.visualPrefabName = kind.VisualId;
            data.unitCount = 12;
            data.leadershipCost = kind.ranged ? 160 : 200;
            data.bodyRadius = 1f;
            data.staminaMax = 150; data.staminaRunCostPerSoldier = 1f; data.staminaRecoveryPerSecond = 10; data.staminaRecoverFraction = 0.3f;
            data.block = data.blockRegenRate = 0; data.shieldBreakStunDuration = 0;
            data.detectionRange = Mathf.Max(reference.detectionRange, 12f);
            data.abilitiesByLevel = new List<AbilityData>();
            kind.data(data);
            if (!kind.ranged) data.rangedData = null;
            AssetDatabase.CreateAsset(data, dataPath);
        }
        string modulePath = $"{kind.Folder}/{kind.key}_{(kind.ranged ? "Ranged" : "Melee")}.asset";
        var module = AssetDatabase.LoadAssetAtPath<ScriptableObject>(modulePath);
        if (!module)
        {
            module = UnityEngine.Object.Instantiate(Load<ScriptableObject>(kind.moduleReference));
            module.name = Path.GetFileNameWithoutExtension(modulePath);
            kind.module(module);
            AssetDatabase.CreateAsset(module, modulePath);
        }
        if (kind.ranged) data.rangedData = (SquadRangedData)module;
        else data.meleeData = (SquadMeleeData)module;

        data.formations = new[]
        {
            Formation(kind, "Line", "Assets/Resources/Squads/Spearmen/Formations/SpearmenLineFormation.asset", 2.6f),
            Formation(kind, "Wedge", "Assets/Resources/Squads/Spearmen/Formations/SpearmenWedgeFormation.asset", 2.8f),
            Formation(kind, "Dispersed", "Assets/Resources/Squads/Spearmen/Formations/SpearmenDispersedFormation.asset", 4f),
        };

        string ecsPath = $"{kind.Folder}/UnitPrefab_ECS_{kind.key}.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(ecsPath))
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(reference.prefab), ecsPath);
        var contents = PrefabUtility.LoadPrefabContents(ecsPath);
        try
        {
            contents.name = "UnitPrefab_ECS_" + kind.key;
            var authoring = contents.GetComponent<UnitEntityAuthoring>();
            authoring.squadData = data;
            authoring.baseSpeed = data.baseSpeed;
            data.prefab = PrefabUtility.SaveAsPrefabAsset(contents, ecsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);

        database.allSquads ??= new List<SquadData>();
        if (database.allSquads.Any(d => d && d != data && d.id == data.id))
            throw new InvalidOperationException("Duplicate squad ID: " + data.id);
        if (!database.allSquads.Contains(data)) database.allSquads.Add(data);

        var visual = Load<GameObject>(kind.Prefab);
        var serialized = new SerializedObject(config);
        var entries = serialized.FindProperty("unitPrefabs");
        int index = -1;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("squadType").intValue == (int)kind.type) { index = i; break; }
        if (index < 0) { index = entries.arraySize; entries.arraySize++; }
        var entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("squadType").intValue = (int)kind.type;
        entry.FindPropertyRelative("unitId").stringValue = kind.VisualId;
        entry.FindPropertyRelative("displayName").stringValue = kind.displayName;
        entry.FindPropertyRelative("visualPrefab").objectReferenceValue = visual;
        entry.FindPropertyRelative("battleVisualPrefab").objectReferenceValue = visual;
        entry.FindPropertyRelative("isDefault").boolValue = true;
        entry.FindPropertyRelative("variants").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>A copy of an infantry formation with the cavalry's spacing, no postures and no brace.</summary>
    static FormationDefinition Formation(Kind kind, string name, string source, float spacing)
    {
        string folder = kind.Folder + "/Formations";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(kind.Folder, "Formations");
        string path = $"{folder}/{kind.key}_{name}.asset";
        var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>(path);
        if (!formation)
        {
            AssetDatabase.CopyAsset(source, path);
            formation = Load<FormationDefinition>(path);
            formation.spacing = spacing;
            formation.postures = Array.Empty<FormationRolePosture>();
            formation.supportsBrace = false;
            formation.useNormalPostureInFreeCombat = false;
            EditorUtility.SetDirty(formation);
            AssetDatabase.SaveAssetIfDirty(formation);
        }
        return formation;
    }
}
