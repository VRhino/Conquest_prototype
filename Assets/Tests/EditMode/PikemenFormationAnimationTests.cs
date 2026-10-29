using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercise the shipped Animator: rank silhouette, two-handed grasp and return to locomotion.</summary>
public class PikemenFormationAnimationTests
{
    const string Folder = "Assets/Resources/Squads/Pikemen";
    const string Layer = "Pike Formation";
    Scene _scene;
    GameObject _actor;
    Animator _animator;
    Transform _pike;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _actor = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Pikemen_GO_Skeleton.prefab"), _scene);
        _actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var b in _actor.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        _animator = _actor.GetComponent<Animator>();
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.Rebind();
        _animator.SetBool(AnimationHashes.IsGrounded, true);
        _animator.SetBool(AnimationHashes.IsStopped, true);
        _pike = _actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "PikeCarrier");
    }

    [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);
    Transform Bone(HumanBodyBones bone) => _animator.GetBoneTransform(bone);
    void Advance() { for (int i = 0; i < 30; i++) _animator.Update(1f / 30); }
    void Enter(UnitStance stance)
    {
        _animator.SetInteger(AnimationHashes.Stance, (int)stance);
        Advance();
        Assert.That(_animator.IsInTransition(_animator.GetLayerIndex(Layer)), Is.False);
    }

    [Test]
    public void CatalogEnablesRecruitmentForExistingHeroesWithoutGrantingTroops()
    {
        SquadDataService.ForceReinitialize();
        var pikemen = SquadDataService.GetSquadById("pkm01");
        var spearmen = SquadDataService.GetSquadById("spm01");
        Assert.That(pikemen, Is.Not.Null);
        Assert.That(pikemen.rarity, Is.EqualTo(spearmen.rarity));
        Assert.That(pikemen.leadershipCost, Is.EqualTo(spearmen.leadershipCost));
        Assert.That(pikemen.unitCount, Is.EqualTo(spearmen.unitCount));
        Assert.That(pikemen.baseHealth, Is.EqualTo(spearmen.baseHealth));
        Assert.That(pikemen.IsMelee, Is.True);
        Assert.That(pikemen.IsRanged, Is.False);
        var oldHero = new HeroData { availableSquads = new System.Collections.Generic.List<string> { "spm01" } };
        Assert.That(SquadDataService.GetSquadsForHero(oldHero.availableSquads, UnitType.Infantry), Does.Contain(pikemen));
        Assert.That(SquadDataService.GetSquadsForHero(oldHero.availableSquads, UnitType.Distance), Has.No.Member(pikemen));
        Assert.That(oldHero.squadProgress, Is.Empty);
        Assert.That(oldHero.availableSquads, Is.EqualTo(new[] { "spm01" }));
        oldHero.availableSquads.Add(pikemen.id);
        Assert.That(SquadDataService.GetSquadsForHero(oldHero.availableSquads).Count(d => d == pikemen), Is.EqualTo(1));
        var instance = SquadDataService.CreateSquadInstance(pikemen.id);
        oldHero.squadProgress.Add(instance);
        var loadout = new LoadoutSaveData { name = "Pike test", isActive = true };
        loadout.squadInstanceIDs.Add(instance.id);
        oldHero.loadouts.Add(loadout);
        var restored = JsonUtility.FromJson<HeroData>(JsonUtility.ToJson(oldHero));
        Assert.That(SquadDataService.convertInstanceIDsToBaseSquadIDs(restored.loadouts[0].squadInstanceIDs, restored.squadProgress),
            Is.EqualTo(new[] { pikemen.id }));
        Assert.That(SquadDataService.ConvertLoadoutToSquadIconData(restored.loadouts[0], restored).Count, Is.EqualTo(1));
        Assert.That(instance.unitsInSquad, Is.EqualTo(20));
        Assert.That(instance.permittedFormationIndexes.Count, Is.EqualTo(pikemen.formations.Length));
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
        Assert.That(config.GetUnitPrefab(SquadType.Pikemen, pikemen.visualPrefabName), Is.EqualTo(
            AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Pikemen_GO_Skeleton.prefab")));
        Assert.That(pikemen.prefab.GetComponent<UnitEntityAuthoring>().squadData, Is.SameAs(pikemen));
    }

    [Test]
    public void FrontKneelsRearStandsAndPikesKeepDifferentElevations()
    {
        Enter(UnitStance.PikeLineFront);
        Assert.That(Bone(HumanBodyBones.Hips).position.y, Is.InRange(0.40f, 0.55f));
        Assert.That(Bone(HumanBodyBones.RightLowerLeg).position.y, Is.InRange(0.02f, 0.09f));
        Assert.That(Bone(HumanBodyBones.LeftFoot).position.z, Is.GreaterThan(0.25f));
        Assert.That(Bone(HumanBodyBones.RightFoot).position.z, Is.LessThan(-0.25f));
        Assert.That(Mathf.Asin(_pike.up.y) * Mathf.Rad2Deg, Is.InRange(10f, 18f));
        Enter(UnitStance.PikeLineRear);
        Assert.That(Bone(HumanBodyBones.Hips).position.y, Is.InRange(0.72f, 0.90f));
        Assert.That(Mathf.Asin(_pike.up.y) * Mathf.Rad2Deg, Is.InRange(-2f, 5f));
        Assert.That(_actor.transform.position, Is.EqualTo(Vector3.zero));
    }

    [TestCase(UnitStance.PikeLineFront)]
    [TestCase(UnitStance.PikeLineRear)]
    public void BothPalmsStayOnTheShaftDuringThrust(UnitStance stance)
    {
        Enter(stance);
        CheckGrip();
        _animator.SetBool(AnimationHashes.IsAttacking, true);
        for (int i = 0; i < 30; i++)
        {
            _animator.Update(1f / 30);
            CheckGrip();
        }
        _animator.SetBool(AnimationHashes.IsAttacking, false);
        Advance();
        CheckGrip();
    }

    void CheckGrip()
    {
        foreach (var pair in new[] {
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal),
            (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal) })
        {
            var wrist = Bone(pair.Item1).position;
            var palm = wrist + (Bone(pair.Item2).position - wrist).normalized * 0.085f;
            Assert.That(Vector3.Cross(palm - _pike.position, _pike.up).magnitude,
                Is.LessThan(0.04f), pair.Item1 + " palm detached from shaft");
        }
    }

    [TestCase(UnitStance.PikeLineFront)]
    [TestCase(UnitStance.PikeLineRear)]
    public void FreeCombatAttacksStandingRegardlessOfPreviousRank(UnitStance previousRank)
    {
        Enter(previousRank);
        Enter(UnitStance.Normal);
        _animator.SetBool(AnimationHashes.IsAttacking, true);
        for (int i = 0; i < 30; i++)
        {
            _animator.Update(1f / 30);
            Assert.That(Bone(HumanBodyBones.Hips).position.y, Is.GreaterThan(0.7f));
            // Carry-to-thrust blends rotate the shaft before the strike window opens.
            // Check the authored grip once that blend completes; standing is required throughout.
            if (!_animator.IsInTransition(_animator.GetLayerIndex(Layer))) CheckGrip();
        }
        Assert.That(_animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(Layer))
            .IsName(Layer + ".Thrust Rear"), Is.True);
        _animator.SetBool(AnimationHashes.IsAttacking, false);
        Advance();
        Assert.That(Bone(HumanBodyBones.Hips).position.y, Is.GreaterThan(0.7f));
    }

    [TestCase(UnitStance.PikeLineFront)]
    [TestCase(UnitStance.PikeLineRear)]
    public void MovingReleasesKneelAndKeepsTwoHandedCarry(UnitStance stance)
    {
        Enter(stance);
        _animator.SetBool(AnimationHashes.IsStopped, false);
        Advance();
        Assert.That(_animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(Layer))
            .IsName(Layer + ".Locomotion"), Is.True);
        Assert.That(Bone(HumanBodyBones.Hips).position.y, Is.GreaterThan(0.65f));
        Assert.That(_pike.up.y, Is.GreaterThan(0.8f));
        CheckGrip();
        Assert.That(_actor.transform.position, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FormationHasTwoStaggeredRanksWithCorrectPostures()
    {
        var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>(Folder + "/Formations/Pikemen_Line.asset");
        Assert.That(formation, Is.Not.Null);
        Assert.That(formation.Validate(), Is.Null);
        var front = formation.templates[0].slots.Where(s => s.row == 0).OrderBy(s => s.position.x).ToArray();
        var rear = formation.templates[0].slots.Where(s => s.row == 1).OrderBy(s => s.position.x).ToArray();
        Assert.That(front.Length, Is.EqualTo(rear.Length));
        Assert.That(front.All(s => s.role == FormationSlotRole.Front), Is.True);
        Assert.That(rear.All(s => s.role == FormationSlotRole.Rear), Is.True);
        Assert.That(rear[0].position.x - front[0].position.x, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(front[0].position.y, Is.GreaterThan(rear[0].position.y));
        Assert.That(formation.postures.Single(p => p.role == FormationSlotRole.Front).formed, Is.EqualTo(UnitStance.PikeLineFront));
        Assert.That(formation.postures.Single(p => p.role == FormationSlotRole.Rear).formed, Is.EqualTo(UnitStance.PikeLineRear));
    }
}
