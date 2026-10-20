#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// Movement baseline scenarios (Docs/Design/Movimiento_Formaciones.md §5.2). They measure, they do not
/// assert gameplay thresholds: each run writes CSV + summary under Logs/MovementMetrics and an MP4 under
/// Recordings/MovementBaseline. Explicit so they never run in the regular suite.
/// </summary>
public partial class BattleSceneIntegrationTests
{
    const float BaselineSettleSeconds = 4f;    // Loading overlay and grounding, as in the movement test.
    const float S1MarchSeconds = 10f;
    const float S1TailSeconds = 4f;
    const int S3Cycles = 6; // Beyond ~36 m backwards the hero is blocked in BattleTestScene.
    const float S3MarchSeconds = 2f;
    const float S3StopSeconds = 3f;

    const float MinimumHeroDisplacement = 2f; // Proves the scripted input reached the hero.
    // March away from the remote enemy (spawned ~40 m ahead in +z) so no scenario turns into combat.
    const Key MarchKey = Key.S;

    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS1StraightMarch()
    {
        var previousInput = UseUnfocusedEditorInput();
        try
        {
            yield return PrepareBaseline();
            using (MovementVideoCapture.Start("S1"))
            {
                MovementMetricsRecorderSystem.Begin("S1", squad);
                var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                yield return new WaitForSeconds(1f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey));
                yield return new WaitForSeconds(S1MarchSeconds);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(S1TailSeconds);
                ReportBaseline(start);
            }
        }
        finally { RestoreInputSettings(previousInput); }
    }

    static void RestoreInputSettings(InputSettings previous)
    {
        // The Input System may destroy a replaced runtime settings object; fall back to defaults then.
        InputSystem.settings = previous != null ? previous : ScriptableObject.CreateInstance<InputSettings>();
    }

    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS3StopAndGo()
    {
        var previousInput = UseUnfocusedEditorInput();
        try
        {
            yield return PrepareBaseline();
            using (MovementVideoCapture.Start("S3"))
            {
                MovementMetricsRecorderSystem.Begin("S3", squad);
                var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                yield return new WaitForSeconds(1f);
                for (int cycle = 0; cycle < S3Cycles; cycle++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey));
                    yield return new WaitForSeconds(S3MarchSeconds);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSeconds(S3StopSeconds);
                }
                ReportBaseline(start);
            }
        }
        finally { RestoreInputSettings(previousInput); }
    }

    const float S4SettleSeconds = 4f;     // Time for a formation change to complete before the next one.
    const float S4MarchSeconds = 3f;
    const float S6ApproachTimeout = 20f;  // March towards the remote squad until a combat reaction starts.
    const float S6MeleeSeconds = 15f;

    /// <summary>S4: formation changes while still and while marching; units cross each other's paths.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS4FormationChange()
    {
        var previousInput = UseUnfocusedEditorInput();
        try
        {
            yield return PrepareBaseline();
            using (MovementVideoCapture.Start("S4"))
            {
                MovementMetricsRecorderSystem.Begin("S4", squad);
                var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                yield return Press(Key.F2);
                yield return new WaitForSeconds(S4SettleSeconds);
                yield return Press(Key.F1);
                yield return new WaitForSeconds(S4SettleSeconds);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey));
                yield return new WaitForSeconds(1f);
                yield return PressWhileHeld(Key.F2, MarchKey);
                yield return new WaitForSeconds(S4MarchSeconds);
                yield return PressWhileHeld(Key.F1, MarchKey);
                yield return new WaitForSeconds(S4MarchSeconds);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(S4SettleSeconds);
                ReportBaseline(start);
            }
        }
        finally { RestoreInputSettings(previousInput); }
    }

    /// <summary>S6: march at the remote squad (+z) until the squad reacts, then hold the melee.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS6Clash()
    {
        var previousInput = UseUnfocusedEditorInput();
        try
        {
            yield return PrepareBaseline();
            using (MovementVideoCapture.Start("S6"))
            {
                MovementMetricsRecorderSystem.Begin("S6", squad);
                var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                float deadline = Time.time + S6ApproachTimeout;
                while (Time.time < deadline && em.GetComponentData<SquadResolvedOrderComponent>(squad).source
                       != OrderSource.CombatReaction)
                    yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(S6MeleeSeconds);
                ReportBaseline(start);
            }
        }
        finally { RestoreInputSettings(previousInput); }
    }

    IEnumerator PressWhileHeld(Key key, Key held)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(held, key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
        yield return null;
    }

    /// <summary>
    /// The editor is driven remotely and usually has no Game view focus; by default the Input System then
    /// drops keyboard input. A temporary settings copy routes all input to the game for this run only,
    /// leaving the project's InputSettings asset untouched.
    /// </summary>
    static InputSettings UseUnfocusedEditorInput()
    {
        var previous = InputSystem.settings;
        var temporary = Object.Instantiate(previous);
        temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = temporary;
        return previous;
    }

    /// <summary>
    /// Baseline scenarios measure movement, not scene health: the scene logs a known error while the
    /// git-ignored hit VFX asset pack is missing, so only these explicit runs ignore logged errors.
    /// </summary>
    void ConfigureMovementBaselineLogging()
    {
        if (TestContext.CurrentContext.Test.Name.StartsWith("MovementBaseline", System.StringComparison.Ordinal))
            LogAssert.ignoreFailingMessages = true;
    }

    IEnumerator PrepareBaseline()
    {
        yield return new WaitForSeconds(BaselineSettleSeconds);
        yield return Until(() =>
        {
            var members = em.GetBuffer<SquadUnitElement>(squad);
            for (int i = 0; i < members.Length; i++)
            {
                var unit = members[i].Value;
                if (!em.HasComponent<NavMeshAgent>(unit)) return false;
                var agent = em.GetComponentObject<NavMeshAgent>(unit);
                if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            }
            return true;
        }, 10f, "One or more local units have no active agent on the scene NavMesh.");
        // The squad does not start in FollowHero; C issues it, as in the hero movement test.
        yield return Press(Key.C);
        yield return Until(() => em.GetComponentData<SquadResolvedOrderComponent>(squad)
            .order == SquadOrderType.FollowHero, 3f, "FollowHero did not reach order resolution.");
        yield return new WaitForSeconds(2f); // Let the squad settle into formation before recording.
    }

    void ReportBaseline(Unity.Mathematics.float3 heroStart)
    {
        var summary = MovementMetricsRecorderSystem.End(out string directory);
        float moved = Unity.Mathematics.math.distance(heroStart.xz,
            em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position.xz);
        Debug.Log($"Movement baseline written to {directory} (hero moved {moved:F1} m)\n{summary}");
        Assert.That(summary.frames, Is.GreaterThan(0), "The recorder did not sample any frame.");
        Assert.That(moved, Is.GreaterThan(MinimumHeroDisplacement), "Scripted input did not move the hero.");
    }
}
#endif
