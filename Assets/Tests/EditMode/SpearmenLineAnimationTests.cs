using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercise the shipped prefab/controller, including the exit back to the original locomotion.</summary>
public class SpearmenLineAnimationTests
{
    const string PrefabPath = "Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab";
    const string SourceControllerPath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Masculine.controller";
    const string Layer = "Spear Line Formation";
    Scene _scene;
    GameObject _actor;
    Animator _animator;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), _scene);
        _actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var behaviour in _actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        _animator = _actor.GetComponent<Animator>();
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.Rebind();
        _animator.SetBool(AnimationHashes.IsGrounded, true);
        _animator.SetBool(AnimationHashes.IsStopped, true);
    }
    [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);

    void Advance()
    {
        for (int i = 0; i < 30; i++) _animator.Update(1f / 30);
    }
    Vector3 Bone(HumanBodyBones bone) => _animator.GetBoneTransform(bone).position;
    Transform Named(string name) => _actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    void Enter(UnitStance stance)
    {
        _animator.SetInteger(AnimationHashes.Stance, (int)stance);
        Advance();
        Assert.That(_animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(Layer)).IsName(
            Layer + (stance == UnitStance.SpearLineFront ? ".Front" : ".Rear")), Is.True);
    }

    [Test]
    public void HumanoidPosesKeepFeetGroundedAndSeparateTheTwoRanks()
    {
        Enter(UnitStance.SpearLineFront);
        float frontHip = Bone(HumanBodyBones.Hips).y;
        Assert.That(frontHip, Is.InRange(0.35f, 0.55f));
        Assert.That(Bone(HumanBodyBones.LeftFoot).y, Is.InRange(0.02f, 0.10f));
        Assert.That(Bone(HumanBodyBones.RightFoot).y, Is.InRange(0.02f, 0.12f));
        Assert.That(Named("SM_Wep_Shield_Long_14").position.z, Is.GreaterThan(Bone(HumanBodyBones.Head).z));
        Enter(UnitStance.SpearLineRear);
        Assert.That(Bone(HumanBodyBones.Hips).y - frontHip, Is.GreaterThan(0.25f));
        Assert.That(Named("SM_Wep_Shield_Long_14").position.y, Is.GreaterThan(Bone(HumanBodyBones.Head).y));
        Assert.That(Bone(HumanBodyBones.LeftFoot).y, Is.InRange(0.02f, 0.10f));
        Assert.That(Bone(HumanBodyBones.RightFoot).y, Is.InRange(0.02f, 0.10f));
        Assert.That(_actor.transform.position, Is.EqualTo(Vector3.zero), "Poses must not move the ECS-owned root.");
    }

    [TestCase(UnitStance.SpearLineFront, "moving")]
    [TestCase(UnitStance.SpearLineRear, "moving")]
    [TestCase(UnitStance.SpearLineFront, "formation")]
    [TestCase(UnitStance.SpearLineRear, "formation")]
    public void ExitReleasesHumanoidAndRestoresEquipment(UnitStance stance, string reason)
    {
        var carriers = new[] { Named("Weapon_Carrier"), Named("Off_Hand_Weapon_Carrier") };
        var positions = carriers.Select(t => t.localPosition).ToArray();
        var rotations = carriers.Select(t => t.localRotation).ToArray();
        Enter(stance);
        if (reason == "moving") _animator.SetBool(AnimationHashes.IsStopped, false);
        else _animator.SetInteger(AnimationHashes.Stance, (int)UnitStance.Normal);
        Advance();
        Assert.That(_animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(Layer)).IsName(Layer + ".Locomotion"), Is.True);
        Assert.That(Bone(HumanBodyBones.Hips).y, Is.GreaterThan(0.65f), "The inactive layer must release Humanoid RootT.");
        for (int i = 0; i < carriers.Length; i++)
        {
            Assert.That(Vector3.Distance(carriers[i].localPosition, positions[i]), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(carriers[i].localRotation, rotations[i]), Is.LessThan(0.01f));
        }
        // The formation can be entered again after the interrupt finishes.
        _animator.SetBool(AnimationHashes.IsStopped, true);
        _animator.SetBool(AnimationHashes.IsAttacking, false);
        Enter(stance);
    }

    [Test]
    public void PosesFollowTheSquadRootAndCanBeInterruptedDuringEntry()
    {
        Enter(UnitStance.SpearLineFront);
        var handLocal = _actor.transform.InverseTransformPoint(Bone(HumanBodyBones.RightHand));
        var rootPosition = new Vector3(17, 2, -8);
        _actor.transform.SetPositionAndRotation(rootPosition, Quaternion.Euler(0, 63, 0));
        Advance();
        Assert.That(Vector3.Distance(Bone(HumanBodyBones.RightHand), _actor.transform.TransformPoint(handLocal)), Is.LessThan(0.001f));
        Assert.That(_actor.transform.position, Is.EqualTo(rootPosition));
        _animator.SetInteger(AnimationHashes.Stance, (int)UnitStance.Normal);
        Advance();
        _animator.SetInteger(AnimationHashes.Stance, (int)UnitStance.SpearLineRear);
        _animator.Update(0.05f);
        _animator.SetBool(AnimationHashes.IsStopped, false);
        Advance();
        Assert.That(_animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(Layer)).IsName(Layer + ".Locomotion"), Is.True);
    }

    const string ThrustLayer = "Spear Thrust";
    const string MeleePath = "Assets/Resources/Squads/Spearmen/Spearmen_Melee.asset";
    const float Frame = 1f / 60;
    void Step(float seconds) { for (float t = 0; t < seconds - Frame * 0.5f; t += Frame) _animator.Update(Frame); }
    Vector3 Local(Vector3 world) => _actor.transform.InverseTransformPoint(world);
    void Stand(UnitStance stance)
    {
        if (stance != UnitStance.Normal) { Enter(stance); return; }
        _animator.SetInteger(AnimationHashes.Stance, (int)UnitStance.Normal);
        Advance();
    }

    /// <summary>The overhand thrust: only the spear arm moves, so the rank keeps its pose (the front one kneeling) and a
    /// soldier out of formation its own stance; the hand starts above the shoulder and drives the tip forward and down.</summary>
    [TestCase(UnitStance.SpearLineFront)]
    [TestCase(UnitStance.SpearLineRear)]
    [TestCase(UnitStance.Normal)]
    public void AThrustDrivesTheSpearForwardAndDownWithoutLeavingTheStance(UnitStance stance)
    {
        Stand(stance);
        int layer = _animator.GetLayerIndex(ThrustLayer);
        Assert.That(layer, Is.GreaterThan(_animator.GetLayerIndex(Layer)), "The thrust sits above the line poses.");
        var melee = AssetDatabase.LoadAssetAtPath<SquadMeleeData>(MeleePath);
        var hitbox = Named("WeaponHitbox");
        var carriers = new[] { Named("Weapon_Carrier"), Named("Off_Hand_Weapon_Carrier") };
        var positions = carriers.Select(t => t.localPosition).ToArray();
        float hips = Bone(HumanBodyBones.Hips).y;
        string pose = _animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(Layer)).IsName(Layer + ".Locomotion")
            ? "Locomotion" : stance == UnitStance.SpearLineFront ? "Front" : "Rear";

        _animator.SetBool(AnimationHashes.IsAttacking, true);
        Step(melee.strikeWindowStart * 0.5f); // Wound up.
        Assert.That(_animator.GetCurrentAnimatorStateInfo(layer).IsName(ThrustLayer + ".Thrust"), Is.True);
        Assert.That(Bone(HumanBodyBones.RightHand).y, Is.GreaterThan(Bone(HumanBodyBones.RightUpperArm).y), "Overhand: the hand above the shoulder.");
        var guard = Local(hitbox.position);
        Step(melee.strikeWindowStart * 0.5f + melee.strikeWindowDuration * 0.5f); // Inside the hit window.
        var reach = Local(hitbox.position);
        Assert.That(reach.z - guard.z, Is.GreaterThan(0.4f), "The spear drives forward into the hit window.");
        Assert.That(reach.y, Is.LessThan(Local(Bone(HumanBodyBones.RightHand)).y), "The tip points down from the hand.");
        Assert.That(Bone(HumanBodyBones.Hips).y, Is.EqualTo(hips).Within(0.03f), "The body keeps its stance.");
        Assert.That(_animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(Layer)).IsName(Layer + "." + pose), Is.True);

        _animator.SetBool(AnimationHashes.IsAttacking, false);
        Advance();
        Assert.That(_animator.GetCurrentAnimatorStateInfo(layer).IsName(ThrustLayer + ".Idle"), Is.True);
        for (int i = 0; i < carriers.Length; i++)
            Assert.That(Vector3.Distance(carriers[i].localPosition, positions[i]), Is.LessThan(0.0001f), "The stance's grip comes back.");
        _animator.SetBool(AnimationHashes.IsAttacking, true);
        Advance();
        Assert.That(_animator.GetCurrentAnimatorStateInfo(layer).IsName(ThrustLayer + ".Thrust"), Is.True, "Every swing replays.");
    }

    /// <summary>The clip and ECS share the rhythm: the spear is at full reach for the whole hit window of the melee data
    /// and well back while winding up, also at another attack speed.</summary>
    [TestCase(1f)]
    [TestCase(1.5f)]
    public void TheSpearIsAtFullReachForTheWholeHitWindow(float attackSpeed)
    {
        const float held = 0.05f, drawn = 0.4f; // m: what "at full reach" and "well back" allow.
        Stand(UnitStance.Normal);
        var melee = AssetDatabase.LoadAssetAtPath<SquadMeleeData>(MeleePath);
        var hitbox = Named("WeaponHitbox");
        _animator.SetFloat(AnimationHashes.AttackSpeed, attackSpeed);
        _animator.SetBool(AnimationHashes.IsAttacking, true);
        float open = melee.strikeWindowStart / attackSpeed, close = (melee.strikeWindowStart + melee.strikeWindowDuration) / attackSpeed;
        float best = float.MinValue, windowLow = float.MaxValue, windup = 0;
        for (float t = Frame; t < melee.attackAnimationDuration / attackSpeed; t += Frame)
        {
            _animator.Update(Frame);
            float z = Local(hitbox.position).z;
            best = Mathf.Max(best, z);
            if (t >= open && t <= close) windowLow = Mathf.Min(windowLow, z);
            if (Mathf.Abs(t - open * 0.5f) < Frame * 0.5f) windup = z;
        }
        Assert.That(_animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(ThrustLayer)).speedMultiplier,
            Is.EqualTo(attackSpeed).Within(1e-4f));
        Assert.That(best - windowLow, Is.LessThan(held), "The reach holds through the hit window.");
        Assert.That(best - windup, Is.GreaterThan(drawn), "Wound up, the spear is drawn back.");
    }

    [Test]
    public void LocomotionClipsAndColliderAttachmentsArePreserved()
    {
        var original = AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath);
        var integrated = (AnimatorController)_animator.runtimeAnimatorController;
        foreach (var clip in original.animationClips) Assert.That(integrated.animationClips, Does.Contain(clip));
        Assert.That(integrated, Is.Not.SameAs(original));
        Assert.That(Named("WeaponHitbox").parent, Is.SameAs(Named("SM_Wep_Spear_06").parent));
        Assert.That(Named("SM_Wep_Shield_Long_14").GetComponent<Collider>(), Is.Not.Null);
        Assert.That(_animator.applyRootMotion, Is.False);
        Assert.That(integrated.animationClips.Where(c => c.name.StartsWith("SpearLine_" )).All(c => c.humanMotion), Is.True);
    }
}
