using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ConquestTactics.Animation;
using ConquestTactics.Visual;

/// <summary>Authoring for the two-handed pike. Reuses the unit pose capture and ECS formation pipeline.
/// All proportions, grasp points and timings live in ArtSource/PikemenLine/settings.json.</summary>
public static class PikemenFormationSetup
{
    public const string SettingsPath = "ArtSource/PikemenLine/settings.json";
    const string LayerName = "Pike Formation";
    const string CarryLayer = "Pike Two Hand Carry";
    const int RankCount = 2;
    const string Stance = nameof(AnimationHashes.Stance);
    const string Stopped = nameof(AnimationHashes.IsStopped);
    const string Attacking = nameof(AnimationHashes.IsAttacking);

    [Serializable] public class Pose
    {
        public float hipDrop, hipBack, hipYaw, lean, twist, gripSeparation, pitch;
        public Vector3 leftFoot, rightFoot, leftKnee, rightKnee, grip, leftElbow, rightElbow;
    }
    [Serializable] public class Settings
    {
        public string prefabPath, sourceActorName, sourceWeaponName, folder, referenceUnitPath, sourceControllerPath, carrierName, previewName;
        public string squadId, squadName, tierReferencePath, meleeReferencePath, visualConfigPath;
        public float attackRange;
        public float transitionSeconds, attackBlendSeconds, sampleRate, clipSeconds, ikReachFraction, palmOffset, thumbCurl, shaftOriginOffset;
        public float tipHitboxLength, tipHitboxWidth, tipHitboxDepth, columnSpacing, rowSpacing, rearColumnOffset;
        public float strikeStart, strikeDuration, attackDuration, attackInterval, thrustDistance;
        public int columns, previewColumns;
        public Vector3 previewOffset;
        public float[] fingerCurl;
        public Pose front, rear, carry;
    }

    public static Settings ReadSettings() => JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath));

    [MenuItem("Conquest/Pikemen/1 - Prepare From Scene")]
    public static void PrepareFromScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
        var s = ReadSettings();
        var source = GameObject.Find(s.sourceActorName);
        var weaponSource = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SingleOrDefault(g => g.name == s.sourceWeaponName);
        if (!source || !weaponSource) throw new InvalidOperationException("Select the prepared Pikemen scene: actor and separate spear must be present.");
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(s.referenceUnitPath);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var actor = UnityEngine.Object.Instantiate(source);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, preview);
            actor.name = s.sourceActorName;
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c in actor.GetComponents<MonoBehaviour>().Where(c => c && !(c is BattleDrakeStudios.ModularCharacters.ModularCharacterManager) && !(c is EntityVisualSync) && !(c is UnitAnimationAdapter) && !(c is UnitAnimatorController)).ToArray())
                UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in actor.GetComponents<CharacterController>()) UnityEngine.Object.DestroyImmediate(c);
            // Remove inherited hero weapon alternatives, including their inactive hitboxes.
            foreach (var carrier in actor.GetComponentsInChildren<Transform>(true).Where(t => t.name == "WeaponCarrier" || t.name == "OffWeaponCarrier" || t.name == s.carrierName).ToArray())
                UnityEngine.Object.DestroyImmediate(carrier.gameObject);
            foreach (var c in actor.GetComponentsInChildren<ShieldHitboxBehaviour>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
            foreach (var c in actor.GetComponentsInChildren<WeaponHitboxBehaviour>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
            var animator = actor.GetComponent<Animator>();
            animator.applyRootMotion = false;
            var grip = new GameObject(s.carrierName).transform;
            grip.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand), false);
            var weapon = UnityEngine.Object.Instantiate(weaponSource, grip);
            weapon.name = s.sourceWeaponName;
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            var scale = weaponSource.transform.lossyScale;
            var parentScale = grip.lossyScale;
            weapon.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            foreach (var c in weapon.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            var meshBounds = weapon.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            var hit = new GameObject("WeaponHitbox");
            hit.transform.SetParent(weapon.transform, false);
            var box = hit.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, meshBounds.max.y - s.tipHitboxLength / scale.y / 2, 0);
            box.size = new Vector3(s.tipHitboxWidth / scale.x, s.tipHitboxLength / scale.y, s.tipHitboxDepth / scale.z);
            hit.AddComponent<WeaponHitboxBehaviour>();
            var rigid = hit.AddComponent<Rigidbody>();
            rigid.isKinematic = true;
            rigid.useGravity = false;
            if (!actor.GetComponent<UnitAnimationAdapter>()) actor.AddComponent<UnitAnimationAdapter>();
            if (!actor.GetComponent<UnitAnimatorController>()) actor.AddComponent<UnitAnimatorController>();
            if (!actor.GetComponent<EntityVisualSync>()) actor.AddComponent<EntityVisualSync>();
            var agent = actor.GetComponent<NavMeshAgent>();
            if (!agent) agent = actor.AddComponent<NavMeshAgent>();
            EditorUtility.CopySerialized(reference.GetComponent<NavMeshAgent>(), agent);
            var capsule = actor.GetComponent<CapsuleCollider>();
            if (!capsule) capsule = actor.AddComponent<CapsuleCollider>();
            EditorUtility.CopySerialized(reference.GetComponent<CapsuleCollider>(), capsule);
            // Save before posing: the T-pose is the neutral bind reference for reproducible authoring.
            string backup = Path.GetDirectoryName(SettingsPath) + "/Pikemen_before_setup.prefab.backup";
            if (File.Exists(s.prefabPath) && !File.Exists(backup)) File.Copy(s.prefabPath, backup);
            PrefabUtility.SaveAsPrefabAsset(actor, s.prefabPath);
            File.WriteAllText(Path.GetDirectoryName(SettingsPath) + "/weapon_scale.json", JsonUtility.ToJson(scale, true));
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Debug.Log("Pikemen visual prepared from scene; source spear scale preserved.");
    }

    [MenuItem("Conquest/Pikemen/2 - Rebuild Poses And Controller")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
        var s = ReadSettings();
        string folder = s.folder + "/Animations";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(s.folder, "Animations");
        var preview = EditorSceneManager.NewPreviewScene();
        var reports = new List<string>();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefabPath);
            foreach (var pair in new[] { ("Front", s.front), ("Rear", s.rear), ("Carry", s.carry) })
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var b in actor.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                actor.GetComponent<Animator>().enabled = false;
                var error = ApplyPose(actor, pair.Item2, s, 0);
                reports.Add(pair.Item1 + " max wrist target error: " + error.ToString("F5") + " m");
                var clip = Capture(actor, "Pike_" + pair.Item1, s);
                SpearmenLineAnimationSetup.SaveClip(clip, folder);
                UnityEngine.Object.DestroyImmediate(actor);
            }
            foreach (var pair in new[] { ("Front", s.front), ("Rear", s.rear) })
            {
                var clip = new AnimationClip { name = "Pike_Thrust_" + pair.Item1, frameRate = s.sampleRate };
                var frames = new[] { 0f, s.strikeStart, s.strikeStart + s.strikeDuration, s.attackDuration };
                var shifts = new[] { 0f, s.thrustDistance, s.thrustDistance, 0f };
                var curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
                for (int f = 0; f < frames.Length; f++)
                {
                    var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                    actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    foreach (var b in actor.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                    actor.GetComponent<Animator>().enabled = false;
                    float error = ApplyPose(actor, pair.Item2, s, shifts[f]);
                    reports.Add("Thrust " + pair.Item1 + " frame " + f + " max wrist target error: " + error.ToString("F5") + " m");
                    var sample = Capture(actor, "sample", s);
                    foreach (var binding in AnimationUtility.GetCurveBindings(sample))
                    {
                        if (!curves.TryGetValue(binding, out var curve)) curves[binding] = curve = new AnimationCurve();
                        curve.AddKey(frames[f], AnimationUtility.GetEditorCurve(sample, binding).Evaluate(0));
                    }
                    UnityEngine.Object.DestroyImmediate(sample);
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                foreach (var kv in curves) AnimationUtility.SetEditorCurve(clip, kv.Key, kv.Value);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                settings.keepOriginalOrientation = settings.keepOriginalPositionXZ = settings.keepOriginalPositionY = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.EnsureQuaternionContinuity();
                SpearmenLineAnimationSetup.SaveClip(clip, folder);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var controller = BuildController(s);
        BuildFormation(s);
        var contents = PrefabUtility.LoadPrefabContents(s.prefabPath);
        try
        {
            contents.GetComponent<Animator>().runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(contents, s.prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        File.WriteAllLines(Path.GetDirectoryName(SettingsPath) + "/pose_report.txt", reports);
        Debug.Log(string.Join("\n", reports));
    }

    static AnimationClip Capture(GameObject actor, string name, Settings s) => SpearmenLineAnimationSetup.CapturePoseClip(actor, name,
        new SpearmenLineAnimationSetup.Settings { clipSeconds = s.clipSeconds, sampleRate = s.sampleRate },
        actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == s.carrierName));

    [MenuItem("Conquest/Pikemen/3 - Show Formation Review")]
    public static void ShowReview()
    {
        var s = ReadSettings();
        var source = GameObject.Find(s.sourceActorName);
        var previous = GameObject.Find(s.previewName);
        if (previous) Undo.DestroyObjectImmediate(previous);
        var group = new GameObject(s.previewName);
        group.tag = "EditorOnly";
        Undo.RegisterCreatedObjectUndo(group, "Pikemen formation review");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefabPath);
        for (int row = 0; row < RankCount; row++)
            for (int col = 0; col < s.previewColumns; col++)
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                actor.name = "Pikemen_" + (row == 0 ? "Front" : "Rear") + "_" + col;
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var b in actor.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                actor.GetComponent<NavMeshAgent>().enabled = false;
                actor.GetComponent<Animator>().enabled = false;
                foreach (var c in actor.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                AssetDatabase.LoadAssetAtPath<AnimationClip>(s.folder + "/Animations/Pike_" + (row == 0 ? "Front" : "Rear") + ".anim").SampleAnimation(actor, 0);
                actor.transform.SetParent(group.transform, true);
                actor.transform.localPosition = SlotPosition(col, row, s.previewColumns, s);
                // Sampling a clip does not record prefab overrides. Persist the review pose
                // and disabled components so reopening the scene does not restore the bind pose.
                foreach (var component in actor.GetComponentsInChildren<Component>(true))
                    if (component) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        group.transform.position = (source ? source.transform.position : Vector3.zero) + s.previewOffset;
        EditorSceneManager.MarkSceneDirty(group.scene);
        Selection.activeGameObject = group;
    }

    static Vector3 SlotPosition(int column, int row, int columns, Settings s) =>
        new Vector3((column - (columns - 1) / 2f) * s.columnSpacing + (row - (RankCount - 1) / 2f) * s.rearColumnOffset,
            0, ((RankCount - 1) / 2f - row) * s.rowSpacing);

    [MenuItem("Conquest/Pikemen/4 - Register Gameplay Placeholders")]
    public static void RegisterGameplay()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
        var s = ReadSettings();
        var reference = AssetDatabase.LoadAssetAtPath<SquadData>(s.tierReferencePath);
        var meleeReference = AssetDatabase.LoadAssetAtPath<SquadMeleeData>(s.meleeReferencePath);
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefabPath);
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>(s.visualConfigPath);
        var database = Resources.Load<SquadDatabase>("Squads/SquadDatabase");
        if (!reference || !meleeReference || !visual || !config || !database)
            throw new InvalidOperationException("Missing Pikemen registration reference.");
        string dataPath = s.folder + "/Pikemen.asset", meleePath = s.folder + "/Pikemen_Melee.asset";
        var data = AssetDatabase.LoadAssetAtPath<SquadData>(dataPath);
        // Initial defaults only: rerunning registration must preserve manual balance changes.
        if (!data)
        {
            data = UnityEngine.Object.Instantiate(reference);
            data.name = "Pikemen";
            data.id = s.squadId;
            data.squadName = s.squadName;
            data.type = SquadType.Pikemen;
            data.unitType = UnitType.Infantry;
            data.availableForRecruitmentByDefault = true;
            data.visualPrefabName = "UnitVisual_Pikemen";
            data.block = data.blockRegenRate = 0;
            data.shieldBreakStunDuration = 0;
            data.rangedData = null;
            AssetDatabase.CreateAsset(data, dataPath);
        }
        var melee = AssetDatabase.LoadAssetAtPath<SquadMeleeData>(meleePath);
        if (!melee)
        {
            melee = UnityEngine.Object.Instantiate(meleeReference);
            melee.name = "Pikemen_Melee";
            melee.piercingDamage = meleeReference.slashingDamage + meleeReference.piercingDamage + meleeReference.bluntDamage;
            melee.piercingPenetration = Mathf.Max(meleeReference.slashingPenetration, meleeReference.piercingPenetration, meleeReference.bluntPenetration);
            melee.slashingDamage = melee.bluntDamage = melee.slashingPenetration = melee.bluntPenetration = 0;
            melee.attackRange = s.attackRange;
            melee.attackInterval = s.attackInterval;
            melee.attackAnimationDuration = s.attackDuration;
            melee.strikeWindowStart = s.strikeStart;
            melee.strikeWindowDuration = s.strikeDuration;
            AssetDatabase.CreateAsset(melee, meleePath);
        }
        data.meleeData = melee;
        BuildFormation(s);
        var line = AssetDatabase.LoadAssetAtPath<FormationDefinition>(s.folder + "/Formations/Pikemen_Line.asset");
        var referenceLine = reference.formations.First(f => f.formationType == FormationType.Line);
        line.formationIcon = referenceLine.formationIcon;
        EditorUtility.SetDirty(line);
        data.formations = new[] { line }.Concat(reference.formations.Where(f => f.formationType == FormationType.Dispersed)).ToArray();
        string ecsPath = s.folder + "/UnitPrefab_ECS_Pikemen.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(ecsPath))
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(reference.prefab), ecsPath);
        var contents = PrefabUtility.LoadPrefabContents(ecsPath);
        try
        {
            contents.name = "UnitPrefab_ECS_Pikemen";
            var authoring = contents.GetComponent<UnitEntityAuthoring>();
            authoring.squadData = data;
            authoring.baseSpeed = data.baseSpeed;
            data.prefab = PrefabUtility.SaveAsPrefabAsset(contents, ecsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        database.allSquads ??= new List<SquadData>();
        if (database.allSquads.Any(d => d && d != data && d.id == data.id))
            throw new InvalidOperationException("Duplicate squad ID: " + data.id);
        if (!database.allSquads.Contains(data)) database.allSquads.Add(data);
        var serialized = new SerializedObject(config);
        var entries = serialized.FindProperty("unitPrefabs");
        int index = -1;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("squadType").intValue == (int)SquadType.Pikemen) { index = i; break; }
        if (index < 0) { index = entries.arraySize; entries.arraySize++; }
        var entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("squadType").intValue = (int)SquadType.Pikemen;
        entry.FindPropertyRelative("unitId").stringValue = data.visualPrefabName;
        entry.FindPropertyRelative("displayName").stringValue = data.squadName;
        entry.FindPropertyRelative("visualPrefab").objectReferenceValue = visual;
        entry.FindPropertyRelative("isDefault").boolValue = true;
        entry.FindPropertyRelative("variants").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (var asset in new UnityEngine.Object[] { data, line, database, config })
        { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
        SquadDataService.ForceReinitialize();
        Debug.Log("Pikemen registered: barracks defaults, catalog, melee module, ECS authoring and visual registry.");
    }

    static void BuildFormation(Settings s)
    {
        string path = s.folder + "/Formations/Pikemen_Line.asset";
        var definition = AssetDatabase.LoadAssetAtPath<FormationDefinition>(path);
        bool created = !definition;
        if (created) definition = ScriptableObject.CreateInstance<FormationDefinition>();
        definition.formationType = FormationType.Line;
        definition.layout = FormationLayoutKind.Template;
        definition.spacing = s.columnSpacing;
        definition.noiseRadius = definition.noiseAngleDegrees = 0;
        definition.useNormalPostureInFreeCombat = true;
        var slots = new List<FormationSlot>();
        for (int row = 0; row < RankCount; row++)
            for (int column = 0; column < s.columns; column++)
            {
                var p = SlotPosition(column, row, s.columns, s) / definition.spacing;
                slots.Add(new FormationSlot { position = new Vector2(p.x, p.z), row = row,
                    role = row == 0 ? FormationSlotRole.Front : FormationSlotRole.Rear });
            }
        definition.templates = new[] { new FormationTemplate { minUnits = 0, slots = slots.ToArray() } };
        definition.postures = new[]
        {
            new FormationRolePosture { role = FormationSlotRole.Front, formed = UnitStance.PikeLineFront, moving = UnitStance.Normal },
            new FormationRolePosture { role = FormationSlotRole.Rear, formed = UnitStance.PikeLineRear, moving = UnitStance.Normal }
        };
        string issue = definition.Validate();
        if (issue != null) throw new InvalidOperationException(issue);
        if (created) AssetDatabase.CreateAsset(definition, path);
        else { EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition); }
    }

    public static float ApplyPose(GameObject actor, Pose p, Settings s, float thrust)
    {
        var a = actor.GetComponent<Animator>();
        Transform Bone(HumanBodyBones b) => a.GetBoneTransform(b);
        var lh = Bone(HumanBodyBones.LeftHand);
        var rh = Bone(HumanBodyBones.RightHand);
        var originalHands = new[] { lh.rotation, rh.rotation };
        var fingerDirections = new[] { (Bone(HumanBodyBones.LeftMiddleProximal).position - lh.position).normalized, (Bone(HumanBodyBones.RightMiddleProximal).position - rh.position).normalized };
        var palmNormals = new[] { -Vector3.Cross(fingerDirections[0], Bone(HumanBodyBones.LeftThumbProximal).position - lh.position).normalized, Vector3.Cross(fingerDirections[1], Bone(HumanBodyBones.RightThumbProximal).position - rh.position).normalized };
        var feet = new[] { Bone(HumanBodyBones.LeftFoot).rotation, Bone(HumanBodyBones.RightFoot).rotation };
        Bone(HumanBodyBones.Hips).position += new Vector3(0, -p.hipDrop, -p.hipBack);
        Bone(HumanBodyBones.Hips).rotation = Quaternion.AngleAxis(p.hipYaw, Vector3.up) * Bone(HumanBodyBones.Hips).rotation;
        var spine = Bone(HumanBodyBones.Spine);
        spine.rotation = Quaternion.Euler(p.lean, p.twist, 0) * spine.rotation;
        var head = Bone(HumanBodyBones.Head);
        head.rotation = Quaternion.AngleAxis(-p.hipYaw - p.twist, Vector3.up) * head.rotation;
        Solve(Bone(HumanBodyBones.LeftUpperLeg), Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.LeftFoot), p.leftFoot, p.leftKnee, s);
        Solve(Bone(HumanBodyBones.RightUpperLeg), Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), p.rightFoot, p.rightKnee, s);
        Bone(HumanBodyBones.LeftFoot).rotation = feet[0];
        Bone(HumanBodyBones.RightFoot).rotation = feet[1];
        var dir = Quaternion.AngleAxis(-p.pitch, Vector3.right) * Vector3.forward;
        var rearGrip = p.grip + dir * thrust;
        var frontGrip = rearGrip + dir * p.gripSeparation;
        var desiredFingers = new[] { Vector3.right, Vector3.left };
        var desiredNormals = new[] { Vector3.up, Vector3.down };
        var hands = new[] { lh, rh };
        var targets = new[] { frontGrip, rearGrip };
        float maxError = 0;
        for (int side = 0; side < hands.Length; side++)
        {
            var target = targets[side] - desiredFingers[side] * s.palmOffset;
            var upper = Bone(side == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = Bone(side == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            maxError = Mathf.Max(maxError, Solve(upper, lower, hands[side], target, side == 0 ? p.leftElbow : p.rightElbow, s));
            hands[side].rotation = Quaternion.LookRotation(desiredFingers[side], desiredNormals[side]) * Quaternion.Inverse(Quaternion.LookRotation(fingerDirections[side], palmNormals[side])) * originalHands[side];
            string suffix = side == 0 ? "" : " 1";
            foreach (var chain in new[] { "IndexFinger_", "Finger_" })
                for (int i = 0; i < s.fingerCurl.Length; i++)
                {
                    var finger = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == chain + (i + 1).ToString("00") + suffix);
                    finger.rotation = Quaternion.AngleAxis(s.fingerCurl[i], hands[side].forward) * finger.rotation;
                }
            var thumb = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Thumb_01" + suffix);
            thumb.rotation = Quaternion.AngleAxis(s.thumbCurl, hands[side].up) * thumb.rotation;
        }
        var carrier = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == s.carrierName);
        carrier.SetPositionAndRotation(rearGrip + dir * s.shaftOriginOffset, Quaternion.LookRotation(Vector3.Cross(Vector3.right, dir), dir));
        return maxError;
    }

    static float Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, Settings s)
    {
        var origin = upper.position;
        float l1 = Vector3.Distance(origin, lower.position), l2 = Vector3.Distance(lower.position, end.position);
        var delta = target - origin;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + Mathf.Epsilon, (l1 + l2) * s.ikReachFraction);
        var axis = delta.normalized;
        var bend = Vector3.ProjectOnPlane(pole - origin, axis).normalized;
        float along = (l1 * l1 - l2 * l2 + distance * distance) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
        var elbow = origin + axis * along + bend * height;
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + axis * distance - lower.position) * lower.rotation;
        return Vector3.Distance(end.position, target);
    }

    static AnimatorController BuildController(Settings s)
    {
        string folder = s.folder + "/Animations", path = folder + "/AC_Pikemen.controller";
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!c) { AssetDatabase.CopyAsset(s.sourceControllerPath, path); c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); }
        SpearmenLineAnimationSetup.Parameter(c, Stance, AnimatorControllerParameterType.Int);
        SpearmenLineAnimationSetup.Parameter(c, Stopped, AnimatorControllerParameterType.Bool);
        SpearmenLineAnimationSetup.Parameter(c, Attacking, AnimatorControllerParameterType.Bool);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(folder + "/PikeUpperBody.mask");
        if (!mask)
        {
            mask = new AvatarMask { name = "PikeUpperBody" };
            AssetDatabase.CreateAsset(mask, folder + "/PikeUpperBody.mask");
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var b in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }) mask.SetHumanoidBodyPartActive(b, true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefabPath);
        mask.AddTransformPath(prefab.transform, true);
        for (int i = 0; i < mask.transformCount; i++) mask.SetTransformActive(i, mask.GetTransformPath(i).Contains(s.carrierName));
        AnimatorStateMachine Layer(string name, AvatarMask m)
        {
            var layer = c.layers.FirstOrDefault(l => l.name == name);
            if (layer == null)
            {
                layer = new AnimatorControllerLayer { name = name, defaultWeight = 1, avatarMask = m, stateMachine = new AnimatorStateMachine { name = name } };
                AssetDatabase.AddObjectToAsset(layer.stateMachine, c); c.AddLayer(layer);
            }
            foreach (var t in layer.stateMachine.anyStateTransitions) layer.stateMachine.RemoveAnyStateTransition(t);
            foreach (var st in layer.stateMachine.states) layer.stateMachine.RemoveState(st.state);
            return layer.stateMachine;
        }
        var carry = Layer(CarryLayer, mask);
        var carryState = carry.AddState("Carry");
        carryState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Pike_Carry.anim");
        carryState.writeDefaultValues = false;
        carry.defaultState = carryState;
        var sm = Layer(LayerName, null);
        var idle = sm.AddState("Locomotion"); idle.writeDefaultValues = true; sm.defaultState = idle;
        var front = State("Front", "Pike_Front");
        var rear = State("Rear", "Pike_Rear");
        var frontAttack = State("Thrust Front", "Pike_Thrust_Front");
        var rearAttack = State("Thrust Rear", "Pike_Thrust_Rear");
        AnimatorState State(string name, string clip)
        {
            var st = sm.AddState(name); st.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" + clip + ".anim"); st.writeDefaultValues = false; return st;
        }
        void Enter(AnimatorState state, bool attack, bool firstRank)
        {
            var t = sm.AddAnyStateTransition(state);
            SpearmenLineAnimationSetup.Transition(t, attack ? s.attackBlendSeconds : s.transitionSeconds);
            t.AddCondition(attack ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, Attacking);
            t.AddCondition(firstRank ? AnimatorConditionMode.Equals : (attack ? AnimatorConditionMode.NotEqual : AnimatorConditionMode.Equals), (int)(firstRank || attack ? UnitStance.PikeLineFront : UnitStance.PikeLineRear), Stance);
            if (!attack) t.AddCondition(AnimatorConditionMode.If, 0, Stopped);
        }
        Enter(front, false, true); Enter(rear, false, false); Enter(frontAttack, true, true); Enter(rearAttack, true, false);
        foreach (var st in new[] { front, rear })
        {
            var moving = st.AddTransition(idle); SpearmenLineAnimationSetup.Transition(moving, s.transitionSeconds); moving.AddCondition(AnimatorConditionMode.IfNot, 0, Stopped); moving.AddCondition(AnimatorConditionMode.IfNot, 0, Attacking);
            var changed = st.AddTransition(idle); SpearmenLineAnimationSetup.Transition(changed, s.transitionSeconds); changed.AddCondition(AnimatorConditionMode.NotEqual, (int)UnitStance.PikeLineFront, Stance); changed.AddCondition(AnimatorConditionMode.NotEqual, (int)UnitStance.PikeLineRear, Stance); changed.AddCondition(AnimatorConditionMode.IfNot, 0, Attacking);
        }
        foreach (var st in new[] { frontAttack, rearAttack })
        {
            var finish = st.AddTransition(idle); SpearmenLineAnimationSetup.Transition(finish, s.transitionSeconds); finish.AddCondition(AnimatorConditionMode.IfNot, 0, Attacking);
        }
        EditorUtility.SetDirty(mask); EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssetIfDirty(mask); AssetDatabase.SaveAssetIfDirty(c);
        return c;
    }
}
