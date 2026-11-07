using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Imports the Blender study as Humanoid poses on the existing spearman avatar.
/// Authoring only: the existing formation/posture/sync systems drive the resulting controller.</summary>
public static class SpearmenLineAnimationSetup
{
    public const string SettingsPath = "ArtSource/SpearmenLine/unity_integration_settings.json";
    const string ControllerName = "AC_Spearmen.controller";
    const string InactiveState = "Locomotion";
    const string StanceParameter = nameof(AnimationHashes.Stance);
    const string StoppedParameter = nameof(AnimationHashes.IsStopped);
    const string AttackParameter = nameof(AnimationHashes.IsAttacking);

    [Serializable] public class Settings
    {
        public string prefabPath, formationPath, sourceControllerPath, outputFolder, poseSourcePath, layerName;
        public string spearObjectName, shieldObjectName, hitboxObjectName;
        public float transitionSeconds, clipSeconds, sampleRate;
        // The thrust takes its rhythm from the squad's melee data, so the clip and the ECS hit window agree.
        public string meleePath, thrustLayerName;
        public float attackBlendSeconds;
        public Thrust thrust;
    }
    /// <summary>Overhand thrust: the wrist above the shoulder, the tip down over the shield rim. Offsets are in the
    /// actor's space (+z forward), measured from the right upper arm of the upright spearman.</summary>
    [Serializable] public class Thrust
    {
        public Vector3 guardOffset, elbowPole;
        public float pitch, reach, pullback, windup, ikReachFraction;
    }
    const string AttackSpeedParameter = nameof(AnimationHashes.AttackSpeed);
    const string PosePrefix = "SpearLine_";
    const string ThrustClip = "SpearThrust";
    const string ThrustState = "Thrust";
    const string ThrustIdleState = "Idle";
    [Serializable] class PoseSource { public Pose[] poses; }
    [Serializable] class Pose { public string name; public Delta[] bones, equipment; }
    [Serializable] class Delta { public string name, kind; public float[] delta; }

    [MenuItem("Conquest/Spearmen/Rebuild Line Formation Animations")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding assets.");
        var settings = JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath));
        var source = JsonUtility.FromJson<PoseSource>(File.ReadAllText(settings.poseSourcePath));
        if (settings.transitionSeconds < 0 || settings.clipSeconds <= 0 || settings.sampleRate <= 0)
            throw new InvalidDataException("Invalid animation timing settings.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(settings.prefabPath);
        var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>(settings.formationPath);
        if (!prefab || !formation || formation.formationType != FormationType.Line || formation.Validate() != null)
            throw new InvalidDataException("Expected the spearmen prefab and a valid Line formation.");
        if (source.poses.Length != 2 || source.poses[0].name != "Front" || source.poses[1].name != "Rear")
            throw new InvalidDataException("Expected Front and Rear exported poses.");
        EnsureFolder(settings.outputFolder);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            var animator = actor.GetComponent<Animator>();
            animator.enabled = false;
            if (!animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidDataException("Spearman must have a valid Humanoid avatar.");
            var transforms = actor.GetComponentsInChildren<Transform>(true);
            var originals = transforms.ToDictionary(t => t, t => t.localToWorldMatrix);
            var locals = transforms.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
            Transform Named(string name) => transforms.Single(t => t.name == name);
            var equipment = new[] { Named(settings.spearObjectName).parent, Named(settings.shieldObjectName).parent };
            foreach (var pose in source.poses)
            {
                foreach (var t in transforms)
                {
                    var original = locals[t];
                    t.localPosition = original.localPosition;
                    t.localRotation = original.localRotation;
                    t.localScale = original.localScale;
                }
                // Deltas are world-space, converted from Blender to Unity coordinates.
                // Use prefab bind matrices: the FBX bone axes and attachment hierarchy differ.
                foreach (var bone in pose.bones.OrderBy(b => Depth(Named(b.name))))
                    ApplyWorld(Named(bone.name), Matrix(bone.delta) * originals[Named(bone.name)]);
                foreach (var gear in pose.equipment)
                {
                    var mesh = Named(gear.kind == "spear" ? settings.spearObjectName : settings.shieldObjectName);
                    // Move the carrier so the mesh AND its sibling WeaponHitbox follow the pose.
                    var meshLocal = Matrix4x4.TRS(mesh.localPosition, mesh.localRotation, mesh.localScale);
                    ApplyWorld(mesh.parent, Matrix(gear.delta) * originals[mesh] * meshLocal.inverse);
                }
                SaveClip(CapturePoseClip(actor, PosePrefix + pose.name, settings, equipment), settings.outputFolder);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        BuildThrustClip(prefab, settings);
        var controller = BuildController(settings);
        var contents = PrefabUtility.LoadPrefabContents(settings.prefabPath);
        try
        {
            contents.GetComponent<Animator>().runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(contents, settings.prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        formation.postures = new[]
        {
            Posture(FormationSlotRole.Front, UnitStance.SpearLineFront),
            Posture(FormationSlotRole.Rear, UnitStance.SpearLineRear),
            Posture(FormationSlotRole.Flank, UnitStance.SpearLineRear),
            Posture(FormationSlotRole.Interior, UnitStance.SpearLineRear)
        };
        EditorUtility.SetDirty(formation);
        AssetDatabase.SaveAssetIfDirty(formation);
        Debug.Log("Spearmen Line integrated: Front/Rear Humanoid clips, controller and role postures.");
    }

    static FormationRolePosture Posture(FormationSlotRole role, UnitStance stance) =>
        new FormationRolePosture { role = role, formed = stance, moving = UnitStance.Normal };

    static AnimatorController BuildController(Settings settings)
    {
        string path = settings.outputFolder + "/" + ControllerName;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller)
        {
            if (!AssetDatabase.CopyAsset(settings.sourceControllerPath, path)) throw new IOException("Cannot copy locomotion controller.");
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }
        Parameter(controller, StanceParameter, AnimatorControllerParameterType.Int);
        Parameter(controller, StoppedParameter, AnimatorControllerParameterType.Bool);
        Parameter(controller, AttackParameter, AnimatorControllerParameterType.Bool);
        int existing = Array.FindIndex(controller.layers, l => l.name == settings.layerName);
        AnimatorStateMachine sm;
        if (existing >= 0)
        {
            sm = controller.layers[existing].stateMachine;
            foreach (var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
            foreach (var state in sm.states) sm.RemoveState(state.state);
        }
        else
        {
            sm = new AnimatorStateMachine { name = settings.layerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);
            controller.AddLayer(new AnimatorControllerLayer { name = settings.layerName, stateMachine = sm,
                defaultWeight = 1, blendingMode = AnimatorLayerBlendingMode.Override });
        }
        var inactive = sm.AddState(InactiveState);
        // An empty state releases Humanoid channels to the base layer. Write Defaults
        // restores the two equipment carriers to their prefab offsets on exit.
        // A Transform-only reset clip would instead zero Humanoid body translation.
        inactive.motion = null;
        inactive.writeDefaultValues = true;
        sm.defaultState = inactive;
        AddPoseState(sm, inactive, settings, "Front", UnitStance.SpearLineFront);
        AddPoseState(sm, inactive, settings, "Rear", UnitStance.SpearLineRear);
        BuildThrustLayer(controller, settings);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return controller;
    }

    /// <summary>Regenerates the thrust and rebuilds both layers, without re-importing the poses from Blender or touching
    /// the prefab and the formation.</summary>
    [MenuItem("Conquest/Spearmen/Rebuild Thrust Animation")]
    public static void RebuildThrust()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding assets.");
        var settings = ReadSettings();
        BuildThrustClip(AssetDatabase.LoadAssetAtPath<GameObject>(settings.prefabPath), settings);
        BuildController(settings);
    }

    public static Settings ReadSettings() => JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath));

    /// <summary>The thrust moves only the spear arm and the spear, on a layer above the Line poses: the front rank strikes
    /// kneeling, the rear ones standing with the shield up, and a soldier out of formation from its own stance. The swing
    /// lasts as long as the ECS swing and runs at the squad's attack speed.</summary>
    static void BuildThrustLayer(AnimatorController controller, Settings settings)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(settings.prefabPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(settings.outputFolder + "/" + ThrustClip + ".anim");
        if (!prefab || !clip) throw new InvalidDataException("Expected the spearman and its thrust clip.");
        string maskPath = settings.outputFolder + "/" + ThrustClip + ".mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (!mask) { mask = new AvatarMask { name = ThrustClip }; AssetDatabase.CreateAsset(mask, maskPath); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i == (int)AvatarMaskBodyPart.RightArm || i == (int)AvatarMaskBodyPart.RightFingers);
        string carrier = AnimationUtility.CalculateTransformPath(
            prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == settings.spearObjectName).parent, prefab.transform);
        mask.AddTransformPath(prefab.transform, true);
        for (int i = 0; i < mask.transformCount; i++) mask.SetTransformActive(i, mask.GetTransformPath(i) == carrier);
        EditorUtility.SetDirty(mask);
        AssetDatabase.SaveAssetIfDirty(mask);

        if (!controller.parameters.Any(p => p.name == AttackSpeedParameter))
            controller.AddParameter(new AnimatorControllerParameter
                { name = AttackSpeedParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == settings.thrustLayerName);
        AnimatorStateMachine sm;
        if (index >= 0)
        {
            sm = layers[index].stateMachine;
            foreach (var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
            foreach (var state in sm.states) sm.RemoveState(state.state);
            layers[index].avatarMask = mask;
            controller.layers = layers;
        }
        else
        {
            sm = new AnimatorStateMachine { name = settings.thrustLayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);
            controller.AddLayer(new AnimatorControllerLayer { name = settings.thrustLayerName, stateMachine = sm,
                avatarMask = mask, defaultWeight = 1, blendingMode = AnimatorLayerBlendingMode.Override });
        }
        // Empty and without Write Defaults: at rest the layer leaves the arm and the spear to the layers below.
        var idle = sm.AddState(ThrustIdleState);
        idle.motion = null;
        idle.writeDefaultValues = false;
        sm.defaultState = idle;
        var thrust = sm.AddState(ThrustState);
        thrust.motion = clip;
        thrust.writeDefaultValues = false;
        thrust.speedParameter = AttackSpeedParameter;
        thrust.speedParameterActive = true;
        var enter = sm.AddAnyStateTransition(thrust);
        Transition(enter, settings.attackBlendSeconds);
        enter.AddCondition(AnimatorConditionMode.If, 0, AttackParameter);
        var finish = thrust.AddTransition(idle);
        Transition(finish, settings.attackBlendSeconds);
        finish.AddCondition(AnimatorConditionMode.IfNot, 0, AttackParameter);
    }

    /// <summary>The clip, keyed on the melee data: guard, wind-up, full reach when the hit window opens, held while it
    /// lasts, back to guard when the swing ends.</summary>
    static void BuildThrustClip(GameObject prefab, Settings settings)
    {
        var melee = AssetDatabase.LoadAssetAtPath<SquadMeleeData>(settings.meleePath);
        var t = settings.thrust;
        if (!prefab || !melee || t == null) throw new InvalidDataException("Expected the spearman, its melee data and thrust settings.");
        float strike = melee.strikeWindowStart, hold = strike + melee.strikeWindowDuration, end = melee.attackAnimationDuration;
        if (strike <= 0 || hold > end || t.windup <= 0 || t.windup >= 1 || t.reach <= 0)
            throw new InvalidDataException("The thrust keys must be ordered inside the swing.");
        var keys = new[] { (0f, 0f), (strike * t.windup, -t.pullback), (strike, t.reach), (hold, t.reach), (end, 0f) };
        var curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var (time, shift) in keys)
            {
                var actor = PoseThrust(prefab, preview, shift, settings, out var carrier);
                var sample = CapturePoseClip(actor, "sample", settings, carrier);
                foreach (var binding in AnimationUtility.GetCurveBindings(sample))
                {
                    if (!curves.TryGetValue(binding, out var curve)) curves[binding] = curve = new AnimationCurve();
                    curve.AddKey(time, AnimationUtility.GetEditorCurve(sample, binding).Evaluate(0));
                }
                UnityEngine.Object.DestroyImmediate(sample);
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var clip = new AnimationClip { name = ThrustClip, frameRate = settings.sampleRate };
        foreach (var kv in curves)
        {
            // Clamped tangents: the held reach must not overshoot past the keys.
            for (int i = 0; i < kv.Value.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(kv.Value, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(kv.Value, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, kv.Key, kv.Value);
        }
        var options = AnimationUtility.GetAnimationClipSettings(clip);
        options.loopTime = false;
        options.keepOriginalOrientation = options.keepOriginalPositionXZ = options.keepOriginalPositionY = true;
        AnimationUtility.SetAnimationClipSettings(clip, options);
        clip.EnsureQuaternionContinuity();
        SaveClip(clip, settings.outputFolder);
    }

    /// <summary>The upright spearman with the spear arm at <paramref name="shift"/> metres along the thrust (negative:
    /// wound up). Only the arm and the spear carrier are baked; the body is whatever the layers below put there.
    /// Public so the thrust can be reviewed before it is baked.</summary>
    public static GameObject PoseThrust(GameObject prefab, Scene scene, float shift, Settings settings, out Transform carrier)
    {
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        var animator = actor.GetComponent<Animator>();
        animator.enabled = false;
        var transforms = actor.GetComponentsInChildren<Transform>(true);
        Transform Named(string name) => transforms.Single(x => x.name == name);
        var spear = Named(settings.spearObjectName);
        carrier = spear.parent;

        var t = settings.thrust;
        var upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var direction = Quaternion.AngleAxis(t.pitch, Vector3.right) * Vector3.forward;
        var shoulder = upper.position;
        SolveTwoBone(upper, animator.GetBoneTransform(HumanBodyBones.RightLowerArm), hand,
            shoulder + t.guardOffset + direction * shift, shoulder + t.elbowPole, t.ikReachFraction);
        // The spear is rigid in the hand: turn the hand until the tip (the end with the hitbox) points along the thrust.
        var hitbox = Named(settings.hitboxObjectName);
        var tip = Vector3.Dot(spear.up, hitbox.position - spear.position) >= 0 ? spear.up : -spear.up;
        hand.rotation = Quaternion.FromToRotation(tip, direction) * hand.rotation;
        return actor;
    }

    /// <summary>Puts the actor in the first frame of a clip made by <see cref="CapturePoseClip"/>: its inverse.</summary>
    public static void ApplyPoseClip(GameObject actor, AnimationClip clip)
    {
        if (!clip) throw new InvalidDataException("Missing Line pose clip.");
        float Value(string path, Type type, string property)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property));
            if (curve == null) throw new InvalidDataException(clip.name + " has no curve " + path + " " + property);
            return curve.Evaluate(0);
        }
        var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
        for (int i = 0; i < HumanTrait.MuscleCount; i++) pose.muscles[i] = Value("", typeof(Animator), MuscleProperty(HumanTrait.MuscleName[i]));
        pose.bodyPosition = new Vector3(Value("", typeof(Animator), "RootT.x"), Value("", typeof(Animator), "RootT.y"), Value("", typeof(Animator), "RootT.z"));
        pose.bodyRotation = new Quaternion(Value("", typeof(Animator), "RootQ.x"), Value("", typeof(Animator), "RootQ.y"),
            Value("", typeof(Animator), "RootQ.z"), Value("", typeof(Animator), "RootQ.w"));
        using (var handler = new HumanPoseHandler(actor.GetComponent<Animator>().avatar, actor.transform)) handler.SetHumanPose(ref pose);
        foreach (var path in AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)).Select(b => b.path).Distinct())
        {
            var target = actor.transform.Find(path);
            target.localPosition = new Vector3(Value(path, typeof(Transform), "m_LocalPosition.x"),
                Value(path, typeof(Transform), "m_LocalPosition.y"), Value(path, typeof(Transform), "m_LocalPosition.z"));
            target.localRotation = new Quaternion(Value(path, typeof(Transform), "m_LocalRotation.x"), Value(path, typeof(Transform), "m_LocalRotation.y"),
                Value(path, typeof(Transform), "m_LocalRotation.z"), Value(path, typeof(Transform), "m_LocalRotation.w"));
        }
    }

    /// <summary>Two-bone IK: places <paramref name="end"/> on <paramref name="target"/> with the middle joint bent towards
    /// <paramref name="pole"/>, never straighter than <paramref name="reachFraction"/>. Returns the remaining error.</summary>
    public static float SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, float reachFraction)
    {
        var origin = upper.position;
        float l1 = Vector3.Distance(origin, lower.position), l2 = Vector3.Distance(lower.position, end.position);
        var delta = target - origin;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + Mathf.Epsilon, (l1 + l2) * reachFraction);
        var axis = delta.normalized;
        var bend = Vector3.ProjectOnPlane(pole - origin, axis).normalized;
        float along = (l1 * l1 - l2 * l2 + distance * distance) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
        var elbow = origin + axis * along + bend * height;
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + axis * distance - lower.position) * lower.rotation;
        return Vector3.Distance(end.position, target);
    }

    static void AddPoseState(AnimatorStateMachine sm, AnimatorState inactive, Settings settings, string name, UnitStance stance)
    {
        var state = sm.AddState(name);
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(settings.outputFolder + "/" + PosePrefix + name + ".anim");
        state.writeDefaultValues = false;
        var enter = sm.AddAnyStateTransition(state);
        Transition(enter, settings.transitionSeconds);
        enter.AddCondition(AnimatorConditionMode.Equals, (int)stance, StanceParameter);
        enter.AddCondition(AnimatorConditionMode.If, 0, StoppedParameter);
        var changed = state.AddTransition(inactive);
        Transition(changed, settings.transitionSeconds);
        changed.AddCondition(AnimatorConditionMode.NotEqual, (int)stance, StanceParameter);
        var moving = state.AddTransition(inactive);
        Transition(moving, settings.transitionSeconds);
        moving.AddCondition(AnimatorConditionMode.IfNot, 0, StoppedParameter);
        // Attacking keeps the pose: the thrust layer above moves only the spear arm.
    }

    public static void Transition(AnimatorStateTransition transition, float seconds)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = seconds;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        // Unordered AnyState interruption can restart this same blend every frame.
        transition.orderedInterruption = true;
    }
    public static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        var existing = controller.parameters.FirstOrDefault(p => p.name == name);
        if (existing == null) controller.AddParameter(name, type);
        else if (existing.type != type) throw new InvalidDataException("Unexpected parameter type: " + name);
    }
    static AnimationClip NewClip(string name, Settings settings)
    {
        var clip = new AnimationClip { name = name, frameRate = settings.sampleRate };
        var options = AnimationUtility.GetAnimationClipSettings(clip);
        options.loopTime = true;
        options.keepOriginalOrientation = true;
        options.keepOriginalPositionXZ = true;
        options.keepOriginalPositionY = true;
        options.loopBlendOrientation = true;
        options.loopBlendPositionXZ = true;
        options.loopBlendPositionY = true;
        AnimationUtility.SetAnimationClipSettings(clip, options);
        return clip;
    }
    static string MuscleProperty(string name)
    {
        // Unity's serialized finger bindings differ from HumanTrait's display names.
        foreach (var side in new[] { "Left", "Right" })
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                string prefix = side + " " + finger + " ";
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return side + "Hand." + finger + "." + name.Substring(prefix.Length);
            }
        return name;
    }
    /// <summary>Shared authoring path: capture a posed unit and its equipment using its actual Humanoid avatar.</summary>
    public static AnimationClip CapturePoseClip(GameObject actor, string name, Settings settings, params Transform[] equipment)
    {
        var animator = actor.GetComponent<Animator>();
        var humanPose = new HumanPose();
        using (var handler = new HumanPoseHandler(animator.avatar, actor.transform)) handler.GetHumanPose(ref humanPose);
        var clip = NewClip(name, settings);
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            Curve(clip, "", typeof(Animator), MuscleProperty(HumanTrait.MuscleName[i]), humanPose.muscles[i], settings.clipSeconds);
        VectorCurve(clip, "", typeof(Animator), "RootT", humanPose.bodyPosition, settings.clipSeconds);
        RotationCurve(clip, "", typeof(Animator), "RootQ", humanPose.bodyRotation, settings.clipSeconds);
        foreach (var carrier in equipment) RecordTransform(clip, actor.transform, carrier, settings.clipSeconds);
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    public static void SaveClip(AnimationClip clip, string folder)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" + clip.name + ".anim");
        if (existing)
        {
            EditorUtility.CopySerialized(clip, existing);
            UnityEngine.Object.DestroyImmediate(clip);
            AssetDatabase.SaveAssetIfDirty(existing);
        }
        else AssetDatabase.CreateAsset(clip, folder + "/" + clip.name + ".anim");
    }
    static void RecordTransform(AnimationClip clip, Transform root, Transform target, float duration)
    {
        string path = AnimationUtility.CalculateTransformPath(target, root);
        VectorCurve(clip, path, typeof(Transform), "m_LocalPosition", target.localPosition, duration);
        RotationCurve(clip, path, typeof(Transform), "m_LocalRotation", target.localRotation, duration);
    }
    static void VectorCurve(AnimationClip clip, string path, Type type, string property, Vector3 v, float duration)
    {
        Curve(clip, path, type, property + ".x", v.x, duration);
        Curve(clip, path, type, property + ".y", v.y, duration);
        Curve(clip, path, type, property + ".z", v.z, duration);
    }
    static void RotationCurve(AnimationClip clip, string path, Type type, string property, Quaternion q, float duration)
    {
        Curve(clip, path, type, property + ".x", q.x, duration);
        Curve(clip, path, type, property + ".y", q.y, duration);
        Curve(clip, path, type, property + ".z", q.z, duration);
        Curve(clip, path, type, property + ".w", q.w, duration);
    }
    static void Curve(AnimationClip clip, string path, Type type, string property, float value, float duration) =>
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), AnimationCurve.Constant(0, duration, value));
    static int Depth(Transform t) => t.parent ? Depth(t.parent) + 1 : 0;
    static Matrix4x4 Matrix(float[] data)
    {
        var matrix = new Matrix4x4();
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++) matrix[row, column] = data[row * 4 + column];
        return matrix;
    }
    static void ApplyWorld(Transform t, Matrix4x4 world) => t.SetPositionAndRotation(world.GetColumn(3), world.rotation);
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
