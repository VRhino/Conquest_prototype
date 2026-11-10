#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// Cavalry showcase (M17), filmed like the movement baselines (Recordings/MovementBaseline, 30 fps fixed step): the
/// lance cavalry still, at a trot, at a gallop behind the running hero, stopping, and charging enemy infantry. A side
/// camera follows the squad. Explicit: it never runs in the regular suite.
/// </summary>
public partial class BattleSceneIntegrationTests
{
    [UnityTest, Explicit, Category("MovementBaseline")]
    public IEnumerator MovementBaselineLanceCavalryShowcase()
    {
        var previousInput = UseUnfocusedEditorInput();
        var cameraObject = new GameObject("CavalryShowcaseCamera");
        try
        {
            yield return PrepareBaseline();
            Assert.That(em.HasComponent<MountComponent>(em.GetBuffer<SquadUnitElement>(squad)[0].Value), Is.True, "The squad is cavalry.");
            using var enemies = em.CreateEntityQuery(typeof(HeroAITag), typeof(NavMeshAgent), typeof(HeroSquadReference));
            yield return Until(() => enemies.CalculateEntityCount() > 0, 10f, "Remote hero did not spawn.");
            Entity enemy = Entity.Null;
            using (var candidates = enemies.ToEntityArray(Unity.Collections.Allocator.Temp))
                enemy = candidates.FirstOrDefault(c => em.GetComponentData<TeamComponent>(c).value == Team.TeamB);
            Assert.That(enemy, Is.Not.EqualTo(Entity.Null));
            testWorld.GetExistingSystemManaged<HeroSkillRequestSystem>().Enabled = false;
            foreach (var participant in new[] { hero, enemy })
            {
                StatStack.SetBase(em, participant, StatId.MaxHealth, 100000f);
                StatStack.MarkDirty(em, participant);
                var health = em.GetComponentData<HeroHealthComponent>(participant);
                health.currentHealth = health.maxHealth = 100000f;
                em.SetComponentData(participant, health);
            }
            var enemySquad = em.GetComponentData<HeroSquadReference>(enemy).squad;

            var camera = cameraObject.AddComponent<ShowcaseCamera>();
            camera.Focus = () => Centroid(squad);
            camera.Snap();
            using (MovementVideoCapture.Start("LanceCavalry"))
            {
                MovementMetricsRecorderSystem.Begin("LanceCavalry", squad);
                var start = em.GetComponentData<LocalTransform>(hero).Position;
                yield return new WaitForSeconds(1.5f);                                    // still: the horses breathe
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey));
                yield return new WaitForSeconds(5f);                                      // march: trot
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey, Key.LeftShift));
                yield return new WaitForSeconds(5f);                                      // the hero runs: gallop
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(3.5f);                                    // stop

                // Enemy infantry 16 m away, back along the open ground the cavalry came by; then the charge. The
                // enemy AI pulls its squad back to its objective, so the cavalry sees further than its 12 m here, to
                // chase it instead of losing it.
                var sight = em.GetComponentData<SquadDefinitionComponent>(squad);
                sight.detectionRange = 40f;
                em.SetComponentData(squad, sight);
                float3 centre = Centroid(squad) ?? (float3)em.GetComponentData<LocalTransform>(hero).Position;
                float3 heading = math.normalizesafe(start - centre, new float3(0, 0, 1));
                heading.y = 0f;
                Place(enemy, centre + heading * 21f);
                var members = em.GetBuffer<SquadUnitElement>(enemySquad).ToNativeArray(Unity.Collections.Allocator.Temp);
                int placed = 0;
                for (int i = 0; i < members.Length; i++)
                {
                    float3 side = math.cross(heading, new float3(0, 1, 0));
                    if (Place(members[i].Value, centre + heading * 16f + side * ((i % 6) - 2.5f) * 1.2f + heading * (i / 6) * 1.2f)) placed++;
                }
                members.Dispose();
                Debug.Log($"[CavalryShowcase] cavalry at {centre}, enemy infantry placed {placed} at {Centroid(enemySquad)}");
                camera.Distance = 22f;
                yield return new WaitForSeconds(0.5f);
                yield return Press(Key.V);                                                // attack order: the charge
                for (float t = 0f; t < 13f; t += 1f)
                {
                    yield return new WaitForSeconds(1f);
                    Debug.Log($"[CavalryShowcase] t={t + 1:0} cavalry {Centroid(squad)} enemy {Centroid(enemySquad)} " +
                              $"order {em.GetComponentData<SquadResolvedOrderComponent>(squad).order}");
                }
                ReportBaseline(start);
            }
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            UnityEngine.Object.Destroy(cameraObject);
            RestoreInputSettings(previousInput);
        }
    }

    /// <summary>Warps a body to the nearest point of the NavMesh around <paramref name="position"/>.</summary>
    bool Place(Entity unit, float3 position)
    {
        var agent = em.GetComponentObject<NavMeshAgent>(unit);
        if (agent == null || !NavMesh.SamplePosition(position, out var hit, 6f, NavMesh.AllAreas) || !agent.Warp(hit.position))
            return false;
        var pose = em.GetComponentData<LocalTransform>(unit);
        pose.Position = agent.transform.position;
        em.SetComponentData(unit, pose);
        return true;
    }

    /// <summary>Centre of the squad's living soldiers, or null.</summary>
    float3? Centroid(Entity squadEntity)
    {
        if (!em.Exists(squadEntity)) return null;
        float3 sum = float3.zero;
        int count = 0;
        foreach (var member in em.GetBuffer<SquadUnitElement>(squadEntity))
        {
            if (!em.Exists(member.Value) || em.HasComponent<IsDeadComponent>(member.Value)) continue;
            sum += em.GetComponentData<LocalTransform>(member.Value).Position;
            count++;
        }
        return count > 0 ? sum / count : null;
    }


    /// <summary>Side camera that follows a point smoothly, drawn over the game camera (higher depth).</summary>
    sealed class ShowcaseCamera : MonoBehaviour
    {
        public Func<float3?> Focus;
        public float Distance = 18f;
        public Vector3 Direction = new Vector3(1f, 0.85f, 0.35f);
        Camera _camera;
        int _scenery;

        void Awake()
        {
            _camera = gameObject.AddComponent<Camera>();
            _camera.depth = 100f;
            _camera.fieldOfView = 40f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 400f;
            // Walls and terrain block the view; bodies and triggers do not.
            _scenery = ~LayerMask.GetMask("Units_A", "Units_B", "UI", "Ignore Raycast");
        }

        public void Snap() => Move(1f);

        void LateUpdate() => Move(1f - Mathf.Exp(-2.5f * Time.deltaTime));

        void Move(float follow)
        {
            var focus = Focus?.Invoke();
            if (!focus.HasValue) return;
            Vector3 target = (Vector3)focus.Value + Vector3.up * 1.4f;
            Vector3 desired = target + Direction.normalized * Distance;
            // A wall between the squad and the camera: come in front of it.
            if (Physics.SphereCast(target, 0.5f, desired - target, out var hit, Distance, _scenery, QueryTriggerInteraction.Ignore))
                desired = target + Direction.normalized * Mathf.Max(4f, hit.distance - 0.5f);
            transform.position = Vector3.Lerp(transform.position, desired, follow);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target - transform.position), follow);
        }
    }
}
#endif
