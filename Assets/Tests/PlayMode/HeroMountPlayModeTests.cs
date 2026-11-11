#if UNITY_EDITOR
using System.Collections;
using ConquestTactics.Visual;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// The hero on horseback (M19) in the battle scene: G calls the horse and the hero gets on, gallops, gets off, and
/// falls when the horse dies. The explicit scenario films it like the movement baselines.
/// </summary>
public partial class BattleSceneIntegrationTests
{
    HeroMountComponent HeroMount => em.GetComponentData<HeroMountComponent>(hero);

    [UnityTest]
    public IEnumerator HeroMountsGallopsDismountsAndFallsWhenItsHorseDies()
    {
        var previousInput = UseUnfocusedEditorInput();
        int missingParameters = 0;
        void Count(string message, string stack, LogType type)
        {
            if (message.StartsWith("Parameter '") && message.Contains("does not exist")) missingParameters++;
        }
        try
        {
            yield return PrepareBaseline();
            Assert.That(VisualSyncUtility.TryGetVisual(testWorld, hero, out var visual), Is.True, "The hero has a visual.");
            var presenter = visual.GetComponent<HeroMountPresenter>();
            Assert.That(presenter, Is.Not.Null, "Heroes get a mount presenter.");
            var animator = visual.GetComponent<Animator>();
            var footController = animator.runtimeAnimatorController;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float3 feet = em.GetComponentData<LocalTransform>(hero).Position;
            Application.logMessageReceived += Count;

            // Getting on: the horse comes beside the hero, which starts the clip where it stood.
            yield return Press(Key.G);
            yield return Until(() => HeroMount.state == HeroMountState.Mounting, 2f, "G did not call the horse.");
            yield return null;
            yield return null;
            Assert.That(presenter.Riding, Is.True, "The horse is shown.");
            StringAssert.StartsWith("AC_HeroRider_", animator.runtimeAnimatorController.name);
            Assert.That(math.distance(((float3)hips.position).xz, feet.xz), Is.LessThan(0.6f), "The hero starts getting on where it stood.");
            Assert.That(math.distance(em.GetComponentData<LocalTransform>(hero).Position.xz, feet.xz), Is.GreaterThan(0.4f),
                "The body moved to the horse's centre.");
            yield return Until(() => HeroMount.state == HeroMountState.Mounted, 3f, "The hero did not finish getting on.");
            yield return new WaitForSeconds(0.4f);
            float ground = em.GetComponentData<LocalTransform>(hero).Position.y;
            Assert.That(hips.position.y - ground, Is.InRange(1.45f, 2.0f), "The rider sits on the saddle.");

            // Galloping away from the enemy: faster than the hero runs, and the horse tires.
            var horse = HeroMount.horse;
            float3 start = em.GetComponentData<LocalTransform>(hero).Position;
            float maxSpeed = 0f, maxBlend = 0f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey, Key.LeftShift));
            for (float t = 0f; t < 3.5f; t += Time.deltaTime)
            {
                yield return null;
                maxSpeed = math.max(maxSpeed, math.length(em.GetComponentData<HeroMotorStateComponent>(hero).velocity.xz));
                maxBlend = math.max(maxBlend, presenter.Horse.Animator.GetFloat(AnimationHashes.MountGaitBlend));
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            float moved = math.distance(start.xz, em.GetComponentData<LocalTransform>(hero).Position.xz);
            Debug.Log($"[HeroMount] galloped {moved:F1} m, top speed {maxSpeed:F1} m/s, gait blend {maxBlend:F2}");
            Assert.That(moved, Is.GreaterThan(12f), "The horse carries the hero.");
            Assert.That(maxSpeed, Is.GreaterThan(9f), "It gallops with Shift.");
            Assert.That(maxBlend, Is.GreaterThan(2.5f), "The horse plays its gallop.");
            Assert.That(em.GetComponentData<StaminaComponent>(horse).currentStamina,
                Is.LessThan(em.GetComponentData<StaminaComponent>(horse).maxStamina), "Galloping tires the horse.");
            yield return new WaitForSeconds(2.5f);

            // Getting off: everything back on foot, beside the horse.
            yield return Press(Key.G);
            yield return Until(() => HeroMount.state == HeroMountState.OnFoot, 3f, "The hero did not get off.");
            yield return null;
            yield return null;
            Assert.That(HeroMount.lastEnd, Is.EqualTo(HeroMountEnd.Dismounted));
            Assert.That(presenter.Riding, Is.False);
            Assert.That(em.Exists(horse), Is.False, "The horse goes.");
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(footController), "The on-foot Animator is back.");
            Assert.That(math.distance(((float3)hips.position).xz, ((float3)visual.transform.position).xz), Is.LessThan(0.5f),
                "The body lands where the hero now stands.");

            // The horse dying throws the hero to the ground; it goes on on foot. (A horse can be called again 1.5 s after
            // getting off.)
            yield return new WaitForSeconds(1.8f);
            yield return Press(Key.G);
            yield return Until(() => HeroMount.state == HeroMountState.Mounted, 4f, "The hero did not get on again.");
            horse = HeroMount.horse;
            var health = em.GetComponentData<HealthComponent>(horse);
            health.currentHealth = 0f;
            em.SetComponentData(horse, health);
            yield return Until(() => HeroMount.state == HeroMountState.OnFoot, 2f, "A dead horse still carries the hero.");
            Assert.That(HeroMount.lastEnd, Is.EqualTo(HeroMountEnd.HorseKilled));
            Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive, Is.True);
            yield return null;
            Assert.That(presenter.Riding, Is.False);
            Assert.That(missingParameters, Is.Zero, "An Animator parameter the hero's controller does not have was set.");
        }
        finally
        {
            Application.logMessageReceived -= Count;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            RestoreInputSettings(previousInput);
        }
    }

    /// <summary>Filmed: the hero gets on, trots, gallops, turns, stops and gets off. Explicit: never in the regular suite.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineHeroMount()
    {
        var previousInput = UseUnfocusedEditorInput();
        var cameraObject = new GameObject("HeroMountCamera");
        try
        {
            yield return PrepareBaseline();
            var camera = cameraObject.AddComponent<ShowcaseCamera>();
            camera.Focus = () => em.GetComponentData<LocalTransform>(hero).Position;
            camera.Distance = 12f;
            // From the hero's left and above, so the rider is seen getting on over the horse's left flank.
            camera.Direction = new Vector3(-1f, 1.1f, 0.3f);
            camera.Snap();
            using (MovementVideoCapture.Start("HeroMount"))
            {
                MovementMetricsRecorderSystem.Begin("HeroMount", squad);
                var start = em.GetComponentData<LocalTransform>(hero).Position;
                float clock = 0f;
                IEnumerator Hold(float seconds, string what)
                {
                    for (float t = 0f; t < seconds; t += 0.5f)
                    {
                        yield return new WaitForSeconds(0.5f);
                        clock += 0.5f;
                        var p = em.GetComponentData<LocalTransform>(hero).Position;
                        Debug.Log($"[HeroMount] t={clock:0.0} {what} pos=({p.x:0.0},{p.z:0.0}) state={HeroMount.state} " +
                                  $"speed={math.length(em.GetComponentData<HeroMotorStateComponent>(hero).velocity.xz):0.0}");
                    }
                }
                yield return Hold(1f, "still");
                yield return Press(Key.G);                                                  // call the horse, get on
                yield return Hold(2f, "mount");
                // A U away from the enemy and back, in the open ground around the start.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey));
                yield return Hold(2.5f, "trot");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey, Key.LeftShift));
                yield return Hold(0.5f, "gallop");
                // Turning before the paved yard: further on, its wall stops the horse dead.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
                yield return Hold(2f, "turn");                                              // at its turn rate
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                yield return Hold(2f, "back");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Hold(2.5f, "stop");
                yield return Press(Key.G);                                                  // get off
                yield return Hold(2f, "dismount");
                Debug.Log($"[HeroMount] last end {HeroMount.lastEnd}");
                ReportBaseline(start);
            }
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Object.Destroy(cameraObject);
            RestoreInputSettings(previousInput);
        }
    }
}
#endif
