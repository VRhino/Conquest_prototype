using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        public string spearObjectName, shieldObjectName;
        public float transitionSeconds, clipSeconds, sampleRate;
    }
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
                SaveClip(CapturePoseClip(actor, "SpearLine_" + pose.name, settings, equipment), settings.outputFolder);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

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
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return controller;
    }

    static void AddPoseState(AnimatorStateMachine sm, AnimatorState inactive, Settings settings, string name, UnitStance stance)
    {
        var state = sm.AddState(name);
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(settings.outputFolder + "/SpearLine_" + name + ".anim");
        state.writeDefaultValues = false;
        var enter = sm.AddAnyStateTransition(state);
        Transition(enter, settings.transitionSeconds);
        enter.AddCondition(AnimatorConditionMode.Equals, (int)stance, StanceParameter);
        enter.AddCondition(AnimatorConditionMode.If, 0, StoppedParameter);
        enter.AddCondition(AnimatorConditionMode.IfNot, 0, AttackParameter);
        var changed = state.AddTransition(inactive);
        Transition(changed, settings.transitionSeconds);
        changed.AddCondition(AnimatorConditionMode.NotEqual, (int)stance, StanceParameter);
        var moving = state.AddTransition(inactive);
        Transition(moving, settings.transitionSeconds);
        moving.AddCondition(AnimatorConditionMode.IfNot, 0, StoppedParameter);
        var attack = state.AddTransition(inactive);
        Transition(attack, settings.transitionSeconds);
        attack.AddCondition(AnimatorConditionMode.If, 0, AttackParameter);
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
