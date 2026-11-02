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
    const float S6ApproachTimeout = 20f;  // March towards the remote squad until the first unit strikes.
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

    const float S4CasualtySpacing = 0.15f; // Between forced deaths, so soldiers fall one by one rather than in a frame.
    static readonly int[] SquiresAliveSteps = { 10, 5 };
    static readonly int[] SpearmenAliveSteps = { 15, 10, 5 };

    /// <summary>S4 with casualties (F1): soldiers die by script down to each template, standing still and marching in turns.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS4CasualtiesLine() => RunCasualties("S4CasualtiesLine", Key.F1, SquiresAliveSteps);

    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS4CasualtiesTestudo() => RunCasualties("S4CasualtiesTestudo", Key.F2, SquiresAliveSteps);

    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS4CasualtiesSpearmenLine() => RunCasualties("S4CasualtiesSpearmenLine", Key.F1, SpearmenAliveSteps);

    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS4CasualtiesSpearmenWedge() => RunCasualties("S4CasualtiesSpearmenWedge", Key.F3, SpearmenAliveSteps);

    IEnumerator RunCasualties(string label, Key formationKey, int[] aliveSteps)
    {
        var previousInput = UseUnfocusedEditorInput();
        try
        {
            yield return PrepareBaseline();
            yield return Press(formationKey); // Not every squad spawns in its first formation.
            yield return new WaitForSeconds(S4SettleSeconds);
            using (MovementVideoCapture.Start(label))
            {
                MovementMetricsRecorderSystem.Begin(label, squad);
                var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                for (int step = 0; step < aliveSteps.Length; step++)
                {
                    bool marching = step % 2 == 1;
                    if (marching)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey));
                        yield return new WaitForSeconds(1f);
                    }
                    yield return KillUntil(aliveSteps[step]);
                    yield return new WaitForSeconds(marching ? S4MarchSeconds : S4SettleSeconds);
                    if (marching) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                yield return new WaitForSeconds(S4SettleSeconds);
                ReportBaseline(start);
            }
        }
        finally { RestoreInputSettings(previousInput); }
    }

    /// <summary>Marks random soldiers of the squad dead (seeded in PrepareBaseline) until <paramref name="alive"/> remain.</summary>
    IEnumerator KillUntil(int alive)
    {
        while (true)
        {
            var units = em.GetBuffer<SquadUnitElement>(squad);
            if (units.Length <= alive) yield break;
            em.AddComponent<IsDeadComponent>(units[UnityEngine.Random.Range(0, units.Length)].Value);
            yield return new WaitForSeconds(S4CasualtySpacing);
        }
    }

    // S5 (Movimiento_Formaciones.md §5.2): a straight corridor 4 m wide; both squads march through it from opposite ends.
    const float S5CorridorLength = 16f;
    const float S5HalfWidth = 2f;
    const float S5HeroLateral = 1f;      // Each hero keeps to one side, so they do not block each other; the anchor sits about this far to the hero's other side, so the two squads meet on the same line.
    const float S5EndGap = 6f;           // Open ground between each hero and its mouth of the corridor, m.
    const float S5WaypointArrival = 1f;
    const float S5RemoteArrival = 4f;    // The remote squad has reached its end of the corridor.
    const float S5RemoteTimeout = 70f;
    const float S5WalkTimeout = 20f;
    const float S5TailSeconds = 6f;

    /// <summary>
    /// S5: the remote squad changes side and walks a straight corridor towards the local one, which walks it the other way.
    /// Both follow their hero in Line, wider than the corridor. Counts destinations shared by the two squads (F17).
    /// </summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS5AlliedCrossing() => RunS5("S5", true);

    /// <summary>S5 with each squad resolving its slots on its own, as before F17.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS5AlliedCrossingWithoutOccupancy() => RunS5("S5NoOccupancy", false);

    IEnumerator RunS5(string label, bool occupancy)
    {
        var previousInput = UseUnfocusedEditorInput();
        var walls = new System.Collections.Generic.List<GameObject>();
        using var tuningQuery = em.CreateEntityQuery(typeof(MovementTuningComponent));
        var tuningEntity = tuningQuery.GetSingletonEntity();
        var previous = em.GetComponentData<MovementTuningComponent>(tuningEntity);
        try
        {
            var tuning = previous;
            tuning.slotOccupancyEnabled = occupancy;
            em.SetComponentData(tuningEntity, tuning);
            yield return PrepareBaseline();
            Unity.Entities.Entity remote;
            using (var query = em.CreateEntityQuery(typeof(HeroAITag), typeof(HeroSquadReference), typeof(NavMeshAgent)))
            {
                yield return Until(() => query.CalculateEntityCount() == 1, 10f, "Remote hero agent missing.");
                remote = query.GetSingletonEntity();
            }
            em.RemoveComponent<HeroAITag>(remote);
            Unity.Entities.Entity remoteSquad = em.GetComponentData<HeroSquadReference>(remote).squad;
            var localTeam = em.GetComponentData<TeamComponent>(hero);
            em.SetComponentData(remote, localTeam);
            em.SetComponentData(remoteSquad, localTeam);
            foreach (var element in em.GetBuffer<SquadUnitElement>(remoteSquad).ToNativeArray(Unity.Collections.Allocator.Temp))
                em.SetComponentData(element.Value, localTeam);
            var intent = em.GetComponentData<SquadAIOrderIntentComponent>(remoteSquad);
            intent.suggestedOrder = SquadOrderType.FollowHero;
            intent.targetEntity = Unity.Entities.Entity.Null;
            em.SetComponentData(remoteSquad, intent);

            // March keys are camera relative: S walks along -forward.
            var cameraTransform = Camera.main.transform;
            var back = Vector3.ProjectOnPlane(-cameraTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            var start = (Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
            BuildS5Corridor(start + right * S5HeroLateral, back, walls);
            yield return new WaitForSeconds(S7CarveSeconds);

            float length = S5EndGap + S5CorridorLength + S5EndGap;
            Vector3 remoteStart = start + back * length + right * (2f * S5HeroLateral);
            Assert.That(NavMesh.SamplePosition(remoteStart, out var remoteHit, 2f, NavMesh.AllAreas), Is.True,
                "No NavMesh at the far end of the corridor.");
            var remoteAgent = em.GetComponentObject<NavMeshAgent>(remote);
            Assert.That(remoteAgent.Warp(remoteHit.position), Is.True, "The remote hero could not enter the far end.");
            remoteAgent.speed = em.GetComponentData<HeroStatsComponent>(remote).baseSpeed;
            remoteAgent.isStopped = false;
            yield return Until(() => Distance(remoteHit.position, remoteSquad) < S5RemoteArrival, S5RemoteTimeout,
                "The remote squad did not reach the far end of the corridor.");

            using (MovementVideoCapture.Start(label))
            {
                MovementMetricsRecorderSystem.Begin(label); // Both squads: each one's stalls and slots matter here.
                // A single path may run outside a wall: the corridor is a long thin polygon of the carved NavMesh.
                Vector3 lane = start + right * (2f * S5HeroLateral);
                var remoteLegs = new[]
                {
                    lane + back * (S5EndGap + S5CorridorLength - S5WaypointArrival),
                    lane + back * (S5EndGap + S5WaypointArrival),
                    lane - back * S5EndGap
                };
                int remoteLeg = 0;
                Assert.That(remoteAgent.SetDestination(remoteLegs[0]), Is.True, "The remote hero cannot reach the corridor.");
                void NextRemoteLeg()
                {
                    if (remoteLeg + 1 >= remoteLegs.Length) return;
                    Vector3 toLeg = remoteLegs[remoteLeg] - remoteAgent.transform.position;
                    toLeg.y = 0f;
                    if (toLeg.magnitude < S5WaypointArrival)
                        Assert.That(remoteAgent.SetDestination(remoteLegs[++remoteLeg]), Is.True, "The remote hero cannot walk the corridor.");
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey));
                float deadline = Time.time + S5WalkTimeout;
                while (Time.time < deadline && Vector3.Dot(
                           (Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position - start, back) < length)
                {
                    NextRemoteLeg();
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                float tailEnd = Time.time + S5TailSeconds;
                while (Time.time < tailEnd)
                {
                    NextRemoteLeg();
                    yield return null;
                }
                Assert.That(Vector3.Dot((Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position - start, back),
                    Is.GreaterThan(length * 0.5f), "The local hero did not cross the corridor.");
                Assert.That(Vector3.Dot((Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(remote).Position - remoteHit.position, -back),
                    Is.GreaterThan(length * 0.5f), "The remote hero did not cross the corridor.");
                ReportBaseline((Unity.Mathematics.float3)start);
            }
        }
        finally
        {
            RestoreInputSettings(previousInput);
            foreach (var wall in walls) if (wall != null) Object.Destroy(wall);
            if (em.Exists(tuningEntity)) em.SetComponentData(tuningEntity, previous);
        }
    }

    /// <summary>The two long walls of the S5 corridor, centred on the line from <paramref name="origin"/> along <paramref name="back"/>.</summary>
    static void BuildS5Corridor(Vector3 origin, Vector3 back, System.Collections.Generic.List<GameObject> walls)
    {
        var across = Vector3.Cross(Vector3.up, back);
        foreach (float side in new[] { -1f, 1f })
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "S5 corridor wall";
            wall.transform.SetPositionAndRotation(
                origin + back * (S5EndGap + 0.5f * S5CorridorLength) + across * (side * (S5HalfWidth + 0.5f * S7WallThickness))
                + Vector3.up * (0.5f * S7WallHeight),
                Quaternion.LookRotation(back, Vector3.up));
            wall.transform.localScale = new Vector3(S7WallThickness, S7WallHeight, S5CorridorLength);
            var obstacle = wall.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            walls.Add(wall);
        }
    }

    /// <summary>
    /// S6: march at the remote squad (+z) until one of our units strikes, then hold the melee. The squad only
    /// pursues within its leash when reacting (C6), so the hero has to carry it into contact.
    /// </summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS6Clash() => RunClash("S6", 1f, true);

    /// <summary>S6 with the enemy contact of before M2 P0/P1 (bodyblock radii of SquadSpawnConfig, no sliding).</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS6ClashLegacyContact() => RunClash("S6Legacy", 1f, false);

    /// <summary>S6 without taking the enemy that blocks the unit as its target (before M2 P4).</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS6ClashNoBlockedRetarget() => RunClash("S6NoBlock", 1f, true, false);

    /// <summary>S6 with melee units still adjusting their approach while they swing (before M2 P5).</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS6ClashNoAttackLock() => RunClash("S6NoLock", 1f, true, attackLock: false);

    /// <summary>
    /// Share of the soft saturation (D6) each attacker counts for in S7: one squad crowds an enemy as three would.
    /// Allied squads are driven by the hero AI (zones, advantage checks) and do not reliably join a clash, so S7 lets
    /// one squad concentrate as three would, which is the case the contact ring exists for (C11, M2, M6). Before L4-04
    /// the same case was a hard cap of 6 attackers per target instead of 2.
    /// </summary>
    const float S7SaturationShare = 1f / 3f;

    /// <summary>S7: S6 with several attackers per enemy, so contact rings fill up.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS7Crowd() => RunClash("S7", S7SaturationShare, true);

    /// <summary>S7 with the enemy contact of before M2 P0/P1.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS7CrowdLegacyContact() => RunClash("S7Legacy", S7SaturationShare, false);

    /// <summary>S7 without taking the enemy that blocks the unit as its target (before M2 P4).</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS7CrowdNoBlockedRetarget() => RunClash("S7NoBlock", S7SaturationShare, true, false);

    /// <summary>S7 with melee units still adjusting their approach while they swing (before M2 P5).</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS7CrowdNoAttackLock() => RunClash("S7NoLock", S7SaturationShare, true, attackLock: false);

    IEnumerator RunClash(string label, float saturationShare, bool enemyContact, bool blockedRetarget = true,
        bool attackLock = true)
    {
        var previousInput = UseUnfocusedEditorInput();
        using var tuningQuery = em.CreateEntityQuery(typeof(MovementTuningComponent));
        var tuningEntity = tuningQuery.GetSingletonEntity();
        var tuning = em.GetComponentData<MovementTuningComponent>(tuningEntity);
        var previous = tuning;
        try
        {
            tuning.targetSaturationPerAttacker = previous.targetSaturationPerAttacker * saturationShare;
            tuning.enemyContactEnabled = enemyContact;
            if (!blockedRetarget) tuning.blockedRetargetTime = 0f;
            tuning.attackMovementLock = attackLock;
            em.SetComponentData(tuningEntity, tuning);
            yield return PrepareBaseline();
            using (MovementVideoCapture.Start(label))
            {
                MovementMetricsRecorderSystem.Begin(label, squad);
                var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                float deadline = Time.time + S6ApproachTimeout;
                while (Time.time < deadline && !AnySquadUnitAttacking())
                    yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(S6MeleeSeconds);
                ReportBaseline(start);
            }
        }
        finally
        {
            RestoreInputSettings(previousInput);
            if (em.Exists(tuningEntity))
            {
                tuning = em.GetComponentData<MovementTuningComponent>(tuningEntity);
                tuning.targetSaturationPerAttacker = previous.targetSaturationPerAttacker;
                tuning.enemyContactEnabled = previous.enemyContactEnabled;
                tuning.blockedRetargetTime = previous.blockedRetargetTime;
                tuning.attackMovementLock = previous.attackMovementLock;
                em.SetComponentData(tuningEntity, tuning);
            }
        }
    }

    const float S9ApproachTimeout = 30f; // The two lines march to their hold points.
    const float S9MeleeSeconds = 15f;
    const float S9HeroStandOff = 7f;     // Each hero stops this far from the clash (they start about 10 m away): a dead hero withdraws its squad.
    const float S9LineGap = 5f;          // Between the two hold points; both squads charge from there.
    const float S9HoldArrival = 1.5f;    // From the mean of a squad to its hold point.
    const float S9ChargeTimeout = 8f;
    const float S9MeleeGrace = 3f;       // Longest pause without a swing at the end of the melee.

    /// <summary>
    /// S9 (P7): two squads in Line clash head-on with their heroes out of the melee. The remote hero's AI is taken off;
    /// both heroes walk to a stand-off behind the clash while their squads hold points ahead of them, then both squads
    /// are ordered to attack and the melee runs. Measures the front
    /// case S6/S7 do not isolate: persistent enemy overlaps with and without a target, rear ranks that step ahead of their
    /// own front line, and the time to the first swing.
    /// </summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS9LineClash() => RunLineClash("S9", true);

    /// <summary>S9 with the melee contact of before M2 (no sliding, no blocked retarget, no lock while swinging).</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS9LineClashLegacy() => RunLineClash("S9Legacy", false);

    IEnumerator RunLineClash(string label, bool m2)
    {
        var previousInput = UseUnfocusedEditorInput();
        using var tuningQuery = em.CreateEntityQuery(typeof(MovementTuningComponent));
        var tuningEntity = tuningQuery.GetSingletonEntity();
        var tuning = em.GetComponentData<MovementTuningComponent>(tuningEntity);
        var previous = tuning;
        NavMeshAgent remoteAgent = null;
        try
        {
            em.SetComponentData(tuningEntity, tuning);
            if (!m2)
            {
                tuning.enemyContactEnabled = false;
                tuning.blockedRetargetTime = 0f;
                tuning.attackMovementLock = false;
                em.SetComponentData(tuningEntity, tuning);
            }
            yield return PrepareBaseline();

            Unity.Entities.Entity remote;
            using (var query = em.CreateEntityQuery(typeof(HeroAITag), typeof(HeroSquadReference), typeof(NavMeshAgent)))
            {
                yield return Until(() => query.CalculateEntityCount() == 1, 10f, "Remote hero agent missing.");
                remote = query.GetSingletonEntity();
            }
            // The test drives the remote hero; its squad follows it in its spawn formation (Line for sqd01).
            em.RemoveComponent<HeroAITag>(remote);
            Unity.Entities.Entity remoteSquad = em.GetComponentData<HeroSquadReference>(remote).squad;
            var intent = em.GetComponentData<SquadAIOrderIntentComponent>(remoteSquad);
            intent.suggestedOrder = SquadOrderType.FollowHero;
            intent.targetEntity = Unity.Entities.Entity.Null;
            em.SetComponentData(remoteSquad, intent);
            remoteAgent = em.GetComponentObject<NavMeshAgent>(remote);
            remoteAgent.speed = em.GetComponentData<HeroStatsComponent>(remote).baseSpeed;
            remoteAgent.isStopped = false;
            yield return new WaitForSeconds(2f); // The remote squad leaves its hold and forms on its hero.

            using (MovementVideoCapture.Start(label))
            {
                MovementMetricsRecorderSystem.Begin(label, squad);
                var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                var remoteStart = em.GetComponentData<Unity.Transforms.LocalTransform>(remote).Position;
                Vector3 centre = (Vector3)(start + remoteStart) * 0.5f;
                Vector3 toRemote = ((Vector3)(remoteStart - start)).normalized;
                float walk = Vector3.Dot(centre - (Vector3)start, toRemote) - S9HeroStandOff;
                Assert.That(walk, Is.GreaterThan(MinimumHeroDisplacement), "The heroes start too close for a stand-off.");
                Assert.That(remoteAgent.SetDestination(centre + toRemote * S9HeroStandOff), Is.True,
                    "The remote hero cannot path to its stand-off.");

                // Hold takes its facing from the hero, so both have to be walking at each other first.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(1f);
                Vector3 localHold = centre - toRemote * (S9LineGap * 0.5f);
                Vector3 remoteHold = centre + toRemote * (S9LineGap * 0.5f);
                var playerIntent = em.GetComponentData<SquadPlayerOrderIntentComponent>(squad);
                playerIntent.orderType = SquadOrderType.HoldPosition;
                playerIntent.holdPosition = localHold;
                em.SetComponentData(squad, playerIntent);
                intent = em.GetComponentData<SquadAIOrderIntentComponent>(remoteSquad);
                intent.suggestedOrder = SquadOrderType.HoldPosition;
                intent.holdPosition = remoteHold;
                em.SetComponentData(remoteSquad, intent);

                float deadline = Time.time + S9ApproachTimeout;
                bool walking = true;
                while (Time.time < deadline
                       && (walking || Distance(localHold, squad) > S9HoldArrival || Distance(remoteHold, remoteSquad) > S9HoldArrival))
                {
                    if (walking && Vector3.Dot((Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position
                            - (Vector3)start, toRemote) >= walk)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        walking = false;
                    }
                    yield return null;
                }
                Assert.That(walking, Is.False, "The local hero did not reach its stand-off.");
                Assert.That(Distance(localHold, squad), Is.LessThanOrEqualTo(S9HoldArrival), "The local line did not arrive.");
                Assert.That(Distance(remoteHold, remoteSquad), Is.LessThanOrEqualTo(S9HoldArrival), "The remote line did not arrive.");

                yield return Press(Key.V);
                intent = em.GetComponentData<SquadAIOrderIntentComponent>(remoteSquad);
                intent.suggestedOrder = SquadOrderType.Attack;
                intent.targetEntity = squad;
                em.SetComponentData(remoteSquad, intent);
                yield return Until(AnySquadUnitAttacking, S9ChargeTimeout, "The two lines never met.");
                // A squad with nothing left to fight goes back to its hero, which is no melee to measure.
                float meleeEnd = Time.time + S9MeleeSeconds;
                float lastSwing = Time.time;
                while (Time.time < meleeEnd)
                {
                    if (AnySquadUnitAttacking()) lastSwing = Time.time;
                    yield return null;
                }
                Assert.That(meleeEnd - lastSwing, Is.LessThan(S9MeleeGrace), "The melee broke off before the end.");
                Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(squad).source, Is.EqualTo(OrderSource.Player),
                    "A combat reaction took over the ordered attack and leashed the squad.");
                Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(remoteSquad).source, Is.EqualTo(OrderSource.AI),
                    "A combat reaction took over the ordered attack and leashed the remote squad.");
                Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive
                    && em.GetComponentData<HeroLifeComponent>(remote).isAlive, Is.True,
                    "A hero died: its squad withdrew and the melee was not measured.");
                ReportBaseline(start);
            }
        }
        finally
        {
            RestoreInputSettings(previousInput);
            if (em.Exists(tuningEntity))
                em.SetComponentData(tuningEntity, previous);
        }
    }

    // S7 bottleneck (Movimiento_Formaciones.md §5.2): an L-shaped corridor 2 m wide (inside the walls) in front of the hero.
    const float S7CorridorStart = 6f;     // Open ground between the hero and the corridor mouth, m.
    const float S7FirstLeg = 10f;         // From the mouth to the centre of the corner, m.
    const float S7SecondLeg = 10f;        // From the corner to the exit, m.
    const float S7HalfWidth = 1f;         // Half the free width between the walls, m.
    const float S7WallThickness = 0.5f;
    const float S7WallHeight = 2f;
    const float S7CarveSeconds = 1f;      // Lets the walls carve the NavMesh before the march.
    const float S7LegTimeout = 15f;
    const float S7TailSeconds = 6f;

    /// <summary>
    /// S7 bottleneck: the hero walks a Line formation through a 2 m corridor with a right-angle turn. Formation slots
    /// fall inside the walls or on their far side; the summary counts destinations that share a cell, destinations
    /// reached only by a detour and units stalled away from their slot (C13, L3-01).
    /// </summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS7Bottleneck() => RunS7Bottleneck("S7Bottleneck", true);

    /// <summary>S7 bottleneck with the per-squad slot validation off: each unit projects its own slot, as before C13.</summary>
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineS7BottleneckWithoutSlotResolve() => RunS7Bottleneck("S7BottleneckNoResolve", false);

    IEnumerator RunS7Bottleneck(string label, bool resolveSlots)
    {
        var previousInput = UseUnfocusedEditorInput();
        var walls = new System.Collections.Generic.List<GameObject>();
        using var tuningQuery = em.CreateEntityQuery(typeof(MovementTuningComponent));
        var tuningEntity = tuningQuery.GetSingletonEntity();
        var tuning = em.GetComponentData<MovementTuningComponent>(tuningEntity);
        bool previousResolve = tuning.slotResolveEnabled;
        try
        {
            tuning.slotResolveEnabled = resolveSlots;
            em.SetComponentData(tuningEntity, tuning);
            yield return PrepareBaseline();
            // March keys are camera relative: S walks along -forward, D along +right.
            var cameraTransform = Camera.main.transform;
            var back = Vector3.ProjectOnPlane(-cameraTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            var start = em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
            BuildS7Corridor((Vector3)start, back, right, walls);
            yield return new WaitForSeconds(S7CarveSeconds);

            using (MovementVideoCapture.Start(label))
            {
                MovementMetricsRecorderSystem.Begin(label, squad);
                yield return WalkUntil(Key.S, back, (Vector3)start, S7CorridorStart + S7FirstLeg);
                var corner = (Vector3)start + back * (S7CorridorStart + S7FirstLeg);
                yield return WalkUntil(Key.D, right, corner, S7HalfWidth + S7SecondLeg + 2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(S7TailSeconds);
                var end = (Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position;
                Assert.That(Vector3.Dot(end - corner, right), Is.GreaterThan(S7SecondLeg * 0.5f),
                    "The hero did not take the turn of the corridor.");
                ReportBaseline(start);
            }
        }
        finally
        {
            RestoreInputSettings(previousInput);
            foreach (var wall in walls) if (wall != null) Object.Destroy(wall);
            if (em.Exists(tuningEntity))
            {
                tuning = em.GetComponentData<MovementTuningComponent>(tuningEntity);
                tuning.slotResolveEnabled = previousResolve;
                em.SetComponentData(tuningEntity, tuning);
            }
        }
    }

    /// <summary>Holds <paramref name="key"/> until the hero has advanced <paramref name="distance"/> along <paramref name="direction"/>.</summary>
    IEnumerator WalkUntil(Key key, Vector3 direction, Vector3 from, float distance)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        float deadline = Time.time + S7LegTimeout;
        while (Time.time < deadline
               && Vector3.Dot((Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(hero).Position - from, direction) < distance)
            yield return null;
    }

    /// <summary>
    /// Walls of an L corridor: the first leg runs along <paramref name="back"/> from the mouth to the corner, the second
    /// along <paramref name="right"/>. Each wall carves the NavMesh and blocks the hero's collider.
    /// </summary>
    static void BuildS7Corridor(Vector3 origin, Vector3 back, Vector3 right, System.Collections.Generic.List<GameObject> walls)
    {
        float w = S7HalfWidth, t = S7WallThickness;
        float mouth = S7CorridorStart, corner = S7CorridorStart + S7FirstLeg;
        // (longitudinal from, to; lateral from, to) in the corridor frame: longitudinal along back, lateral along right.
        Wall(mouth, corner + w + t, -w - t, -w);                  // Outer wall of the first leg, up to the corner.
        Wall(mouth, corner - w, w, w + t);                        // Inner wall of the first leg, open at the turn.
        Wall(corner + w, corner + w + t, -w - t, w + S7SecondLeg); // Outer wall of the second leg.
        Wall(corner - w - t, corner - w, w, w + S7SecondLeg);      // Inner wall of the second leg.

        void Wall(float along0, float along1, float across0, float across1)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "S7 corridor wall";
            wall.transform.SetPositionAndRotation(
                origin + back * (0.5f * (along0 + along1)) + right * (0.5f * (across0 + across1)) + Vector3.up * (0.5f * S7WallHeight),
                Quaternion.LookRotation(back, Vector3.up));
            // Local z runs along back, local x along -right for LookRotation(back); the box is symmetric, so only sizes matter.
            wall.transform.localScale = new Vector3(across1 - across0, S7WallHeight, along1 - along0);
            var obstacle = wall.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            walls.Add(wall);
        }
    }

    /// <summary>Distance on the ground from <paramref name="point"/> to the mean position of the squad's living units.</summary>
    float Distance(Vector3 point, Unity.Entities.Entity targetSquad)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var element in em.GetBuffer<SquadUnitElement>(targetSquad))
        {
            if (!em.Exists(element.Value)) continue;
            sum += (Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(element.Value).Position;
            count++;
        }
        if (count == 0) return float.MaxValue;
        Vector3 offset = sum / count - point;
        offset.y = 0f;
        return offset.magnitude;
    }

    bool AnySquadUnitAttacking()
    {
        foreach (var element in em.GetBuffer<SquadUnitElement>(squad))
            if (em.Exists(element.Value) && em.HasComponent<UnitCombatComponent>(element.Value)
                && em.GetComponentData<UnitCombatComponent>(element.Value).isAttacking)
                return true;
        return false;
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
    /// git-ignored hit VFX asset pack is missing, so only these explicit runs ignore logged errors. They also
    /// run at a fixed step from the start (M10).
    /// </summary>
    void ConfigureMovementBaseline()
    {
        previousCaptureFramerate = Time.captureFramerate;
        if (!TestContext.CurrentContext.Test.Name.StartsWith("MovementBaseline", System.StringComparison.Ordinal))
            return;
        LogAssert.ignoreFailingMessages = true;
        // M10: the recorder fixed the step only while filming, so scene load and preparation ran at the
        // editor's real frame rate and every run reached the measurement in a different state. Fix it from
        // the start; TearDown restores it.
        Time.captureFramerate = (int)MovementVideoCapture.FrameRate;
    }

    /// <summary>Random seed of the fixture; baseline scenarios reapply it after loading so their rolls do not depend on setup.</summary>
    const int BaselineRandomSeed = 7319;
    int previousCaptureFramerate;

    IEnumerator PrepareBaseline()
    {
        UnityEngine.Random.InitState(BaselineRandomSeed);
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
