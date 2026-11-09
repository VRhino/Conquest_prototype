using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authoring of the squires' shield formations: the shield wall and the testudo. One Humanoid clip per shield posture,
/// made in Unity: a base pose (the standing or crouching idle) with the left arm moved by two-bone IK until the shield
/// sits where the settings ask. Writes the clips, the two layers of the squires' controller and the posture tables of
/// both formations. Every value lives in ArtSource/SquiresShield/settings.json.
/// </summary>
public static class SquiresShieldAnimationSetup
{
    public const string SettingsPath = "ArtSource/SquiresShield/settings.json";
    const string StanceParameter = nameof(AnimationHashes.Stance);
    const string StoppedParameter = nameof(AnimationHashes.IsStopped);
    public const string ClipPrefix = "Squires_";
    public const string RestState = "Rest";
    const string ShieldMaskName = "Squires_ShieldArm";
    const string SwordMaskName = "Squires_SwordArm";

    /// <summary>One shield posture. Positions and directions are in the actor's space: feet at the origin, +z forward.</summary>
    [Serializable] public class ShieldPose
    {
        /// <summary>The <see cref="UnitStance"/> it is for.</summary>
        public string stance;
        /// <summary>Base pose: the crouching idle instead of the standing one.</summary>
        public bool crouched;
        /// <summary>Centre of the shield. With <see cref="plantOnGround"/> its height is worked out instead.</summary>
        public Vector3 center;
        /// <summary>Where the outer face of the shield looks, and where its top edge points.</summary>
        public Vector3 face, up;
        /// <summary>The elbow bends towards this point, given from the left shoulder.</summary>
        public Vector3 elbowPole;
        /// <summary>Lower the shield until its bottom rests on the ground.</summary>
        public bool plantOnGround;
        /// <summary>The sword hand's grip and where the blade points: kept clear of the neighbours, never into them.</summary>
        public Vector3 swordGrip, swordBlade, swordElbowPole;
    }

    [Serializable] public class Settings
    {
        public string prefabPath, controllerPath, outputFolder, standingClipPath, crouchingClipPath, shieldObjectName;
        public string swordObjectName, swordHitboxName;
        public string shieldWallPath, testudoPath, bodyLayerName, swordLayerName, shieldLayerName, combatLayerName;
        public float transitionSeconds, clipSeconds, sampleRate, ikReachFraction, reachTolerance, shieldWallSpacing;
        public ShieldPose[] poses;
    }

    public static Settings ReadSettings() => JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath));

    public static UnitStance StanceOf(ShieldPose pose) => (UnitStance)Enum.Parse(typeof(UnitStance), pose.stance);

    [MenuItem("Conquest/Squires/Rebuild Shield Formations")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding assets.");
        var s = ReadSettings();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefabPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(s.controllerPath);
        var wall = AssetDatabase.LoadAssetAtPath<FormationDefinition>(s.shieldWallPath);
        var testudo = AssetDatabase.LoadAssetAtPath<FormationDefinition>(s.testudoPath);
        if (!prefab || !controller || !wall || !testudo || s.poses == null || s.poses.Length == 0)
            throw new InvalidDataException("Expected the squire, its controller, both formations and the shield poses.");
        if (s.transitionSeconds < 0 || s.clipSeconds <= 0 || s.sampleRate <= 0 || s.shieldWallSpacing <= 0)
            throw new InvalidDataException("Invalid timing or spacing settings.");
        if (!AssetDatabase.IsValidFolder(s.outputFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(s.outputFolder).Replace('\\', '/'), Path.GetFileName(s.outputFolder));

        var capture = new SpearmenLineAnimationSetup.Settings { clipSeconds = s.clipSeconds, sampleRate = s.sampleRate };
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var pose in s.poses)
            {
                var actor = PoseShield(prefab, preview, pose, s);
                var clip = SpearmenLineAnimationSetup.CapturePoseClip(actor, ClipPrefix + pose.stance, capture);
                UnityEngine.Object.DestroyImmediate(actor);
                SpearmenLineAnimationSetup.SaveClip(clip, s.outputFolder);
                CheckRoundTrip(prefab, preview, pose, s);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        BuildLayers(controller, s);
        WritePostures(wall, testudo, s);
        Debug.Log("Squires shield formations rebuilt: " + s.poses.Length + " poses, controller layers and posture tables.");
    }

    /// <summary>The squire in the pose's base (standing or crouching idle) with the left arm moved so the shield sits where
    /// the pose asks. Public so the poses can be reviewed and tested before they are baked.</summary>
    public static GameObject PoseShield(GameObject prefab, Scene scene, ShieldPose pose, Settings s)
    {
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        var animator = actor.GetComponent<Animator>();
        animator.enabled = false;
        if (!animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
            throw new InvalidDataException("The squire must have a valid Humanoid avatar.");
        SampleHumanoid(actor, AssetDatabase.LoadAssetAtPath<AnimationClip>(pose.crouched ? s.crouchingClipPath : s.standingClipPath), 0f);

        var upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var shield = Shield(actor, s);
        // The shield hangs rigidly from the hand: where the shield must be says where the hand must be.
        var handToShield = hand.worldToLocalMatrix * shield.localToWorldMatrix;
        var rotation = Quaternion.LookRotation(pose.face.normalized, pose.up.normalized);
        var center = pose.center;
        if (pose.plantOnGround) center.y = -LowestOffset(shield, rotation).y;
        var shieldWorld = Matrix4x4.TRS(center - rotation * LocalCenter(shield), rotation, shield.lossyScale);
        var handWorld = shieldWorld * handToShield.inverse;
        var shoulder = upper.position;
        SpearmenLineAnimationSetup.SolveTwoBone(upper, lower, hand, handWorld.GetColumn(3), shoulder + pose.elbowPole, s.ikReachFraction);
        hand.rotation = handWorld.rotation;
        PlaceSword(actor, pose, s);
        return actor;
    }

    /// <summary>The sword hand at the pose's grip, the blade turned along <see cref="ShieldPose.swordBlade"/>.</summary>
    static void PlaceSword(GameObject actor, ShieldPose pose, Settings s)
    {
        var animator = actor.GetComponent<Animator>();
        var upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        SpearmenLineAnimationSetup.SolveTwoBone(upper, lower, hand, pose.swordGrip, upper.position + pose.swordElbowPole, s.ikReachFraction);
        hand.rotation = Quaternion.FromToRotation(SwordFrame(actor, s).blade, pose.swordBlade.normalized) * hand.rotation;
    }

    /// <summary>The sword's grip end and the direction of its blade (towards the end with the hitbox), in world space.</summary>
    public static (Vector3 tip, Vector3 blade) SwordFrame(GameObject actor, Settings s)
    {
        var transforms = actor.GetComponentsInChildren<Transform>(true);
        var sword = transforms.Single(t => t.name == s.swordObjectName);
        var hitbox = transforms.Single(t => t.name == s.swordHitboxName);
        var bounds = sword.GetComponent<MeshFilter>().sharedMesh.bounds;
        // The long axis of the mesh is the blade; its sign is the one that reaches the hitbox.
        var size = bounds.size;
        var axis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        var world = sword.TransformDirection(axis).normalized;
        if (Vector3.Dot(world, hitbox.position - sword.TransformPoint(bounds.center)) < 0) { world = -world; axis = -axis; }
        var tip = sword.TransformPoint(bounds.center + Vector3.Scale(axis, bounds.extents));
        return (tip, world);
    }

    /// <summary>The shield's centre, outer face and top edge, in world space.</summary>
    public static (Vector3 center, Vector3 face, Vector3 up) ShieldFrame(GameObject actor, Settings s)
    {
        var shield = Shield(actor, s);
        var bounds = shield.GetComponent<MeshFilter>().sharedMesh.bounds;
        return (shield.TransformPoint(bounds.center), shield.forward, shield.up);
    }

    /// <summary>The lowest point of the shield mesh, in world space.</summary>
    public static float ShieldBottom(GameObject actor, Settings s)
    {
        var shield = Shield(actor, s);
        return shield.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v => shield.TransformPoint(v).y);
    }

    static Transform Shield(GameObject actor, Settings s) =>
        actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == s.shieldObjectName);

    // The mesh's outer face is its +z and its top its +y (the point of the heater shield is at -y).
    static Vector3 LocalCenter(Transform shield) =>
        Vector3.Scale(shield.GetComponent<MeshFilter>().sharedMesh.bounds.center, shield.lossyScale);

    /// <summary>Offset from the shield's centre to its lowest vertex once turned to <paramref name="rotation"/>.</summary>
    static Vector3 LowestOffset(Transform shield, Quaternion rotation)
    {
        var mesh = shield.GetComponent<MeshFilter>().sharedMesh;
        var center = LocalCenter(shield);
        return mesh.vertices.Select(v => rotation * (Vector3.Scale(v, shield.lossyScale) - center)).OrderBy(v => v.y).First();
    }

    /// <summary>The baked clip must put the shield where the pose asks: the muscle limits of the avatar can pull the arm
    /// away from the IK solution, and an unreachable shield must fail here, not look wrong in battle.</summary>
    static void CheckRoundTrip(GameObject prefab, Scene scene, ShieldPose pose, Settings s)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(s.outputFolder + "/" + ClipPrefix + pose.stance + ".anim");
        var wanted = PoseShield(prefab, scene, pose, s);
        var target = ShieldFrame(wanted, s).center;
        UnityEngine.Object.DestroyImmediate(wanted);
        var baked = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        baked.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        SpearmenLineAnimationSetup.ApplyPoseClip(baked, clip);
        float error = Vector3.Distance(ShieldFrame(baked, s).center, target);
        UnityEngine.Object.DestroyImmediate(baked);
        if (error > s.reachTolerance)
            throw new InvalidDataException($"{pose.stance}: the baked shield lands {error:F3} m from where it was asked; move it within reach.");
    }

    /// <summary>Puts the actor in a Humanoid clip at <paramref name="time"/>. Muscles the clip does not key stay at 0.</summary>
    public static void SampleHumanoid(GameObject actor, AnimationClip clip, float time)
    {
        if (!clip) throw new InvalidDataException("Missing base pose clip.");
        var curves = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Animator) && b.path == "")
            .ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(clip, b));
        float Value(string property) => curves.TryGetValue(property, out var curve) ? curve.Evaluate(time) : 0f;
        var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            pose.muscles[i] = Value(SpearmenLineAnimationSetup.MuscleProperty(HumanTrait.MuscleName[i]));
        pose.bodyPosition = new Vector3(Value("RootT.x"), Value("RootT.y"), Value("RootT.z"));
        pose.bodyRotation = new Quaternion(Value("RootQ.x"), Value("RootQ.y"), Value("RootQ.z"), Value("RootQ.w"));
        if (pose.bodyRotation == new Quaternion(0, 0, 0, 0)) throw new InvalidDataException(clip.name + " has no body rotation.");
        using (var handler = new HumanPoseHandler(actor.GetComponent<Animator>().avatar, actor.transform)) handler.SetHumanPose(ref pose);
    }

    /// <summary>
    /// Three layers on the squires' controller. Below Combat: the whole body crouches for the braced shield wall while the
    /// soldier stands still, and the sword arm keeps the blade clear of the neighbours in every shield posture (a swing on
    /// Combat takes the arm over). Above Combat: the shield arm takes every shield posture, moving or not, so a sword swing
    /// never drops the shield and a formation can advance behind it. Each rests in an empty state with Write Defaults,
    /// which hands the body back to the layers below.
    /// </summary>
    static void BuildLayers(AnimatorController controller, Settings s)
    {
        SpearmenLineAnimationSetup.Parameter(controller, StanceParameter, AnimatorControllerParameterType.Int);
        SpearmenLineAnimationSetup.Parameter(controller, StoppedParameter, AnimatorControllerParameterType.Bool);
        var shieldMask = Mask(s, ShieldMaskName, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.LeftFingers);
        var swordMask = Mask(s, SwordMaskName, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.RightFingers);

        var body = Layer(controller, s.bodyLayerName, null);
        foreach (var pose in s.poses.Where(p => p.crouched))
            PoseState(body.sm, body.rest, pose, s, requireStopped: true);
        var sword = Layer(controller, s.swordLayerName, swordMask);
        foreach (var pose in s.poses)
            PoseState(sword.sm, sword.rest, pose, s, requireStopped: false);
        var shield = Layer(controller, s.shieldLayerName, shieldMask);
        foreach (var pose in s.poses)
            PoseState(shield.sm, shield.rest, pose, s, requireStopped: false);

        // Body and sword arm right below Combat, in that order; the shield arm on top of everything.
        var layers = controller.layers.ToList();
        var bodyLayer = layers.Single(l => l.name == s.bodyLayerName);
        var swordLayer = layers.Single(l => l.name == s.swordLayerName);
        var shieldLayer = layers.Single(l => l.name == s.shieldLayerName);
        layers.Remove(bodyLayer);
        layers.Remove(swordLayer);
        layers.Remove(shieldLayer);
        int combat = layers.FindIndex(l => l.name == s.combatLayerName);
        if (combat < 0) throw new InvalidDataException("The squires' controller has no " + s.combatLayerName + " layer.");
        layers.Insert(combat, swordLayer);
        layers.Insert(combat, bodyLayer);
        layers.Add(shieldLayer);
        controller.layers = layers.ToArray();
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }

    static AvatarMask Mask(Settings s, string name, params AvatarMaskBodyPart[] parts)
    {
        string path = s.outputFolder + "/" + name + ".mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (!mask) { mask = new AvatarMask { name = name }; AssetDatabase.CreateAsset(mask, path); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, parts.Contains((AvatarMaskBodyPart)i));
        mask.transformCount = 0;
        EditorUtility.SetDirty(mask);
        AssetDatabase.SaveAssetIfDirty(mask);
        return mask;
    }

    static (AnimatorStateMachine sm, AnimatorState rest) Layer(AnimatorController controller, string name, AvatarMask mask)
    {
        var layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == name);
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
            sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);
            controller.AddLayer(new AnimatorControllerLayer { name = name, stateMachine = sm, avatarMask = mask,
                defaultWeight = 1, blendingMode = AnimatorLayerBlendingMode.Override });
        }
        var rest = sm.AddState(RestState);
        rest.motion = null;
        rest.writeDefaultValues = true;
        sm.defaultState = rest;
        return (sm, rest);
    }

    static void PoseState(AnimatorStateMachine sm, AnimatorState rest, ShieldPose pose, Settings s, bool requireStopped)
    {
        int stance = (int)StanceOf(pose);
        var state = sm.AddState(pose.stance);
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(s.outputFolder + "/" + ClipPrefix + pose.stance + ".anim");
        if (!state.motion) throw new InvalidDataException("Missing shield clip " + pose.stance + ".");
        state.writeDefaultValues = false;
        var enter = sm.AddAnyStateTransition(state);
        SpearmenLineAnimationSetup.Transition(enter, s.transitionSeconds);
        enter.AddCondition(AnimatorConditionMode.Equals, stance, StanceParameter);
        if (requireStopped) enter.AddCondition(AnimatorConditionMode.If, 0, StoppedParameter);
        var changed = state.AddTransition(rest);
        SpearmenLineAnimationSetup.Transition(changed, s.transitionSeconds);
        changed.AddCondition(AnimatorConditionMode.NotEqual, stance, StanceParameter);
        if (!requireStopped) return;
        var moving = state.AddTransition(rest);
        SpearmenLineAnimationSetup.Transition(moving, s.transitionSeconds);
        moving.AddCondition(AnimatorConditionMode.IfNot, 0, StoppedParameter);
    }

    /// <summary>Shield wall: the first rank braced, the others over it. Testudo: front, sides and roof as authored. Both
    /// break formation to move and to fight freely, so marching and free combat use the Normal posture; Hold Position
    /// keeps the shields.</summary>
    static void WritePostures(FormationDefinition wall, FormationDefinition testudo, Settings s)
    {
        wall.useNormalPostureInFreeCombat = testudo.useNormalPostureInFreeCombat = true;
        wall.formationType = FormationType.ShieldWall;
        wall.spacing = s.shieldWallSpacing;
        wall.noiseRadius = 0f;
        wall.noiseAngleDegrees = 0f;
        wall.postures = new[]
        {
            Posture(FormationSlotRole.Front, UnitStance.BracedShields),
            Posture(FormationSlotRole.Flank, UnitStance.ShieldWallRear),
            Posture(FormationSlotRole.Interior, UnitStance.ShieldWallRear),
            Posture(FormationSlotRole.Rear, UnitStance.ShieldWallRear)
        };
        testudo.postures = new[]
        {
            Posture(FormationSlotRole.Front, UnitStance.ShieldFront),
            Posture(FormationSlotRole.Flank, UnitStance.ShieldSide),
            Posture(FormationSlotRole.Interior, UnitStance.ShieldUp),
            Posture(FormationSlotRole.Rear, UnitStance.ShieldUp)
        };
        foreach (var formation in new[] { wall, testudo })
        {
            string issue = formation.Validate();
            if (issue != null) throw new InvalidDataException(formation.name + ": " + issue);
            EditorUtility.SetDirty(formation);
            AssetDatabase.SaveAssetIfDirty(formation);
        }
    }

    static FormationRolePosture Posture(FormationSlotRole role, UnitStance formed) =>
        new FormationRolePosture { role = role, formed = formed, moving = UnitStance.Normal };
}
