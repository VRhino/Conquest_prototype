using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The "Hit Reaction" layer (L1-09) of the hero's and the troops' controllers: full body, override, on top of every other
/// layer, so a reaction also covers the formation and weapon layers. Its rest state is empty (like the other layers' rests)
/// and lets the layers below through; <c>TriggerHitReaction</c> with <c>HitReactionType</c> (a <see cref="HitReaction"/>)
/// enters the reaction's state from any state, so a new one restarts it, and the state goes back to rest when its clip
/// ends. On controllers with <c>IsDead</c> a death cuts the reaction so the death clip below shows.
/// The clips are Humanoid, made in Blender (Assets/Art/Animations/Humanoid): idle to idle and in place.
/// </summary>
public static class HitReactionAnimatorSetup
{
    public const string LayerName = "Hit Reaction";
    public const string RestState = "None";
    public const string ClipFolder = "Assets/Art/Animations/Humanoid";
    const string TriggerParameter = nameof(AnimationHashes.TriggerHitReaction);
    const string TypeParameter = nameof(AnimationHashes.HitReactionType);
    const string DeadParameter = nameof(AnimationHashes.IsDead);
    const float EnterSeconds = 0.08f;
    const float ExitSeconds = 0.2f;

    public static readonly string[] Controllers =
    {
        "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Masculine.controller",
        "Assets/Resources/Squads/Squires/AC_Unit_Combat.controller",
        "Assets/Resources/Squads/Levy_Archers/AC_Unit_Combat.controller",
        "Assets/Resources/Squads/Spearmen/Animations/AC_Spearmen.controller",
        "Assets/Resources/Squads/Pikemen/Animations/AC_Pikemen.controller",
    };

    /// <summary>Each reaction and its clip; the state is named after the reaction.</summary>
    public static readonly (HitReaction reaction, string clip)[] Reactions =
    {
        (HitReaction.Stagger, "Hit_Stagger"),
        (HitReaction.Knockdown, "Hit_Knockdown"),
        (HitReaction.Launch, "Hit_Launch"),
        (HitReaction.GuardBreak, "Hit_GuardBreak"),
    };

    [MenuItem("Conquest/Animation/Rebuild Hit Reaction Layers")]
    public static void RebuildAll()
    {
        foreach (var path in Controllers)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                ?? throw new FileNotFoundException("Missing controller " + path);
            Build(controller);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Hit reactions: layer \"{LayerName}\" rebuilt in {Controllers.Length} controllers.");
    }

    public static AnimationClip Clip(string name)
    {
        string path = $"{ClipFolder}/{name}.fbx";
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"))
               ?? throw new FileNotFoundException("Missing hit reaction clip " + path);
    }

    public static void Build(AnimatorController controller)
    {
        SpearmenLineAnimationSetup.Parameter(controller, TriggerParameter, AnimatorControllerParameterType.Trigger);
        SpearmenLineAnimationSetup.Parameter(controller, TypeParameter, AnimatorControllerParameterType.Int);
        bool hasDead = controller.parameters.Any(p => p.name == DeadParameter && p.type == AnimatorControllerParameterType.Bool);

        var sm = TopLayer(controller);
        var rest = sm.AddState(RestState);
        rest.motion = null;
        rest.writeDefaultValues = true;
        sm.defaultState = rest;
        foreach (var (reaction, clipName) in Reactions)
        {
            var clip = Clip(clipName);
            var state = sm.AddState(reaction.ToString());
            state.motion = clip;
            state.writeDefaultValues = false;

            var enter = sm.AddAnyStateTransition(state);
            SpearmenLineAnimationSetup.Transition(enter, EnterSeconds);
            enter.canTransitionToSelf = true;   // a new reaction restarts the clip
            enter.AddCondition(AnimatorConditionMode.If, 0, TriggerParameter);
            enter.AddCondition(AnimatorConditionMode.Equals, (int)reaction, TypeParameter);
            if (hasDead) enter.AddCondition(AnimatorConditionMode.IfNot, 0, DeadParameter);

            var done = state.AddTransition(rest);
            done.hasExitTime = true;
            done.exitTime = Mathf.Clamp01(1f - ExitSeconds / Mathf.Max(clip.length, ExitSeconds));
            done.hasFixedDuration = true;
            done.duration = ExitSeconds;

            if (!hasDead) continue;
            var dead = state.AddTransition(rest);
            SpearmenLineAnimationSetup.Transition(dead, 0.1f);
            dead.AddCondition(AnimatorConditionMode.If, 0, DeadParameter);
        }
        EditorUtility.SetDirty(controller);
    }

    /// <summary>The layer, emptied, and last in the list (on top). Created if missing.</summary>
    static AnimatorStateMachine TopLayer(AnimatorController controller)
    {
        var layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == LayerName);
        if (index < 0)
        {
            var created = new AnimatorStateMachine { name = LayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(created, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = LayerName, stateMachine = created, defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Override,
            });
            return created;
        }
        var layer = layers[index];
        var sm = layer.stateMachine;
        foreach (var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
        foreach (var state in sm.states) sm.RemoveState(state.state);
        layer.avatarMask = null;
        layer.defaultWeight = 1f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        var ordered = layers.Where((_, i) => i != index).Append(layer).ToArray();
        controller.layers = ordered;
        return sm;
    }
}
