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
    [TestCase(UnitStance.SpearLineFront, "attack")]
    [TestCase(UnitStance.SpearLineRear, "attack")]
    [TestCase(UnitStance.SpearLineFront, "formation")]
    [TestCase(UnitStance.SpearLineRear, "formation")]
    public void ExitReleasesHumanoidAndRestoresEquipment(UnitStance stance, string reason)
    {
        var carriers = new[] { Named("Weapon_Carrier"), Named("Off_Hand_Weapon_Carrier") };
        var positions = carriers.Select(t => t.localPosition).ToArray();
        var rotations = carriers.Select(t => t.localRotation).ToArray();
        Enter(stance);
        if (reason == "moving") _animator.SetBool(AnimationHashes.IsStopped, false);
        else if (reason == "attack") _animator.SetBool(AnimationHashes.IsAttacking, true);
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
