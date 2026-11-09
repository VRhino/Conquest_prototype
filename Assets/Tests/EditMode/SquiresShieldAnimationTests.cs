using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// F3, squires: the shipped controller puts the shield where the shield wall and the testudo ask for it, through the
/// Stance parameter (never Animator.Play). The shield arm sits above Combat, so a swing never drops the shield; the sword
/// arm and the crouch sit below it, so a swing still plays.
/// </summary>
public class SquiresShieldAnimationTests
{
    const string SquiresPath = "Assets/Resources/Squads/Squires/Squires.asset";
    const float ShieldTolerance = 0.08f;    // m: the live locomotion idle breathes a little against the authored base.
    const float FaceToleranceDegrees = 15f;
    const float BladeToleranceDegrees = 20f;
    const float GroundTolerance = 0.06f;
    const float CrouchDrop = 0.2f;          // m: the braced first rank sits at least this much lower than standing.
    const float SwingMinimum = 0.1f;        // m: the sword hand leaves its rest grip by at least this much in a swing.
    const float ReleaseTolerance = 0.002f;
    static readonly UnitStance[] ShieldStances =
        { UnitStance.BracedShields, UnitStance.ShieldWallRear, UnitStance.ShieldFront, UnitStance.ShieldSide, UnitStance.ShieldUp };

    SquiresShieldAnimationSetup.Settings _settings;
    Scene _scene;
    GameObject _actor;
    Animator _animator;

    [SetUp]
    public void SetUp()
    {
        _settings = SquiresShieldAnimationSetup.ReadSettings();
        _scene = EditorSceneManager.NewPreviewScene();
        _actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(_settings.prefabPath), _scene);
        _actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var behaviour in _actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        _animator = _actor.GetComponent<Animator>();
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.Rebind();
        _animator.SetBool(AnimationHashes.IsGrounded, true);
        _animator.SetBool(AnimationHashes.IsStopped, true);
    }
    [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);

    void Advance(int frames = 30)
    {
        for (int i = 0; i < frames; i++) _animator.Update(1f / 30);
    }
    Vector3 Bone(HumanBodyBones bone) => _animator.GetBoneTransform(bone).position;
    SquiresShieldAnimationSetup.ShieldPose PoseOf(UnitStance stance) =>
        _settings.poses.Single(p => SquiresShieldAnimationSetup.StanceOf(p) == stance);
    bool InState(string layer, string state) =>
        _animator.GetCurrentAnimatorStateInfo(_animator.GetLayerIndex(layer)).IsName(layer + "." + state);
    void Enter(UnitStance stance)
    {
        _animator.SetInteger(AnimationHashes.Stance, (int)stance);
        Advance();
        Assert.That(InState(_settings.shieldLayerName, stance.ToString()), Is.True, "shield arm in " + stance);
        Assert.That(InState(_settings.swordLayerName, stance.ToString()), Is.True, "sword arm in " + stance);
    }
    /// <summary>Where the authoring put the shield for this posture (planted shields resolve their height there).</summary>
    (Vector3 center, Vector3 face) Authored(UnitStance stance)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var posed = SquiresShieldAnimationSetup.PoseShield(AssetDatabase.LoadAssetAtPath<GameObject>(_settings.prefabPath),
                scene, PoseOf(stance), _settings);
            var frame = SquiresShieldAnimationSetup.ShieldFrame(posed, _settings);
            return (frame.center, frame.face);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(UnitStance.BracedShields)]
    [TestCase(UnitStance.ShieldWallRear)]
    [TestCase(UnitStance.ShieldFront)]
    [TestCase(UnitStance.ShieldSide)]
    [TestCase(UnitStance.ShieldUp)]
    public void EachPostureHoldsTheShieldWhereItWasAuthored(UnitStance stance)
    {
        var authored = Authored(stance);
        Enter(stance);
        var live = SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings);
        Assert.That(Vector3.Distance(live.center, authored.center), Is.LessThan(ShieldTolerance), "shield centre");
        Assert.That(Vector3.Angle(live.face, authored.face), Is.LessThan(FaceToleranceDegrees), "shield face");
        Assert.That(Vector3.Angle(SquiresShieldAnimationSetup.SwordFrame(_actor, _settings).blade, PoseOf(stance).swordBlade),
            Is.LessThan(BladeToleranceDegrees), "the blade stays clear of the neighbours");
        Assert.That(SquiresShieldAnimationSetup.SwordFrame(_actor, _settings).tip.y, Is.GreaterThan(-GroundTolerance), "sword tip above the ground");
        Assert.That(_actor.transform.position, Is.EqualTo(Vector3.zero), "Poses must not move the ECS-owned root.");
    }

    [Test]
    public void TheBracedFirstRankCrouchesBehindAShieldPlantedOnTheGround()
    {
        Advance();
        float standing = Bone(HumanBodyBones.Hips).y;
        Enter(UnitStance.BracedShields);
        Assert.That(InState(_settings.bodyLayerName, nameof(UnitStance.BracedShields)), Is.True);
        Assert.That(standing - Bone(HumanBodyBones.Hips).y, Is.GreaterThan(CrouchDrop));
        Assert.That(Mathf.Abs(SquiresShieldAnimationSetup.ShieldBottom(_actor, _settings)), Is.LessThan(GroundTolerance));
        Assert.That(SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings).center.z, Is.GreaterThan(Bone(HumanBodyBones.Head).z),
            "the shield stands in front of the head");
        // Walking stands the body up again.
        _animator.SetBool(AnimationHashes.IsStopped, false);
        Advance();
        Assert.That(InState(_settings.bodyLayerName, SquiresShieldAnimationSetup.RestState), Is.True);
    }

    /// <summary>The shield's vertices in world space.</summary>
    Vector3[] ShieldVertices()
    {
        var shield = _actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == _settings.shieldObjectName);
        return shield.GetComponent<MeshFilter>().sharedMesh.vertices.Select(shield.TransformPoint).ToArray();
    }

    /// <summary>Seen from the front the two ranks close the wall with no gap between their shields, and the second rank's
    /// shield, one row behind, stays clear of the crouched first rank's head.</summary>
    [Test]
    public void TheShieldWallRanksCloseTheWallWithoutTouchingEachOther()
    {
        Enter(UnitStance.BracedShields);
        var frontHead = Bone(HumanBodyBones.Head);
        float frontShieldTop = ShieldVertices().Max(v => v.y);
        Enter(UnitStance.ShieldWallRear);
        var rear = ShieldVertices();
        Assert.That(rear.Min(v => v.y), Is.LessThan(frontShieldTop), "no gap between the two ranks' shields");
        Assert.That(rear.Max(v => v.y), Is.GreaterThan(Bone(HumanBodyBones.Head).y), "the raised shield covers its own head");
        // The first rank stands one row (one spacing) ahead of the second.
        Assert.That(rear.Max(v => v.z), Is.LessThan(frontHead.z + _settings.shieldWallSpacing), "clear of the crouched head");
        Assert.That(SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings).face.y, Is.GreaterThan(0f),
            "the face looks forward and up, over the wall");
    }

    [Test]
    public void TheTestudoRoofIsOverTheHead()
    {
        Enter(UnitStance.ShieldUp);
        var roof = SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings);
        Assert.That(SquiresShieldAnimationSetup.ShieldBottom(_actor, _settings), Is.GreaterThan(Bone(HumanBodyBones.Head).y));
        Assert.That(Vector3.Angle(roof.face, Vector3.up), Is.LessThan(FaceToleranceDegrees));
    }

    /// <summary>At Normal every shield layer rests in its empty Write Defaults state: the soldier looks exactly as with
    /// the layers off (a masked rest without Write Defaults freezes the arm in a default pose).</summary>
    [Test]
    public void AtRestTheShieldLayersLeaveTheSoldierToTheLayersBelow()
    {
        Enter(UnitStance.ShieldUp);
        _animator.SetInteger(AnimationHashes.Stance, (int)UnitStance.Normal);
        Advance();
        var bones = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Hips, HumanBodyBones.Head };
        var withLayers = bones.Select(Bone).ToArray();
        foreach (var layer in new[] { _settings.bodyLayerName, _settings.swordLayerName, _settings.shieldLayerName })
            _animator.SetLayerWeight(_animator.GetLayerIndex(layer), 0f);
        _animator.Update(0f);
        for (int i = 0; i < bones.Length; i++)
            Assert.That(Vector3.Distance(Bone(bones[i]), withLayers[i]), Is.LessThan(ReleaseTolerance), bones[i].ToString());
    }

    /// <summary>The swing turns the torso, and the shield goes with it; what must not move is the shield arm on the chest.</summary>
    [TestCase(UnitStance.BracedShields)]
    [TestCase(UnitStance.ShieldWallRear)]
    [TestCase(UnitStance.ShieldFront)]
    public void ASwingMovesTheSwordButNotTheShieldArm(UnitStance stance)
    {
        Assert.That(_animator.GetLayerIndex(_settings.shieldLayerName), Is.GreaterThan(_animator.GetLayerIndex(_settings.combatLayerName)));
        Assert.That(_animator.GetLayerIndex(_settings.swordLayerName), Is.LessThan(_animator.GetLayerIndex(_settings.combatLayerName)));
        Enter(stance);
        var chest = _animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? _animator.GetBoneTransform(HumanBodyBones.Chest);
        var shield = chest.InverseTransformPoint(SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings).center);
        var grip = Bone(HumanBodyBones.RightHand);
        _animator.SetBool(AnimationHashes.IsAttacking, true);
        float furthest = 0f, shieldDrift = 0f;
        for (int i = 0; i < 30; i++)
        {
            _animator.Update(1f / 30);
            furthest = Mathf.Max(furthest, Vector3.Distance(Bone(HumanBodyBones.RightHand), grip));
            var onChest = chest.InverseTransformPoint(SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings).center);
            shieldDrift = Mathf.Max(shieldDrift, Vector3.Distance(onChest, shield) * chest.lossyScale.x);
        }
        Assert.That(InState(_settings.shieldLayerName, stance.ToString()), Is.True);
        Assert.That(furthest, Is.GreaterThan(SwingMinimum), "the sword swings");
        Assert.That(shieldDrift, Is.LessThan(ShieldTolerance), "the shield arm holds its place on the chest");
    }

    /// <summary>The testudo's Advance walks with the shields as they are when still: the shield arm does not need the
    /// soldier to stop, only its posture.</summary>
    [TestCase(UnitStance.ShieldFront)]
    [TestCase(UnitStance.ShieldSide)]
    [TestCase(UnitStance.ShieldUp)]
    public void TheTestudoShieldsHoldWhileWalking(UnitStance stance)
    {
        Enter(stance);
        var still = SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings).face;
        _animator.SetBool(AnimationHashes.IsStopped, false);
        _animator.SetBool(AnimationHashes.IsWalking, true);
        _animator.SetFloat(AnimationHashes.MoveSpeed, 1f);
        Advance();
        Assert.That(InState(_settings.shieldLayerName, stance.ToString()), Is.True);
        Assert.That(Vector3.Angle(SquiresShieldAnimationSetup.ShieldFrame(_actor, _settings).face, still), Is.LessThan(FaceToleranceDegrees));
    }

    [Test]
    public void SquiresFormShieldWallAndTestudoAndBreakThemToMove()
    {
        var squires = AssetDatabase.LoadAssetAtPath<SquadData>(SquiresPath);
        Assert.That(squires.formations.Select(f => f.formationType),
            Is.EqualTo(new[] { FormationType.ShieldWall, FormationType.Testudo }), "no Line: the squires' line is their shield wall");
        var wall = squires.formations[0];
        Assert.That(wall.postures.Single(p => p.role == FormationSlotRole.Front).formed, Is.EqualTo(UnitStance.BracedShields));
        Assert.That(wall.postures.Where(p => p.role != FormationSlotRole.Front).Select(p => p.formed),
            Is.All.EqualTo(UnitStance.ShieldWallRear));
        foreach (var formation in squires.formations)
        {
            Assert.That(formation.postures.Select(p => p.moving), Is.All.EqualTo(UnitStance.Normal), formation.name + " moves broken up");
            Assert.That(formation.useNormalPostureInFreeCombat, Is.True, formation.name + " fights freely broken up");
        }
        foreach (var stance in ShieldStances)
            Assert.That(_settings.poses.Count(p => SquiresShieldAnimationSetup.StanceOf(p) == stance), Is.EqualTo(1), stance + " has one pose");
    }
}
