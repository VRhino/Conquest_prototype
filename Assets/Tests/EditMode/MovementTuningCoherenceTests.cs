using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class MovementTuningCoherenceTests
{
    const string DefaultAssetPath = "Assets/ScriptableObjects/Movement/DefaultMovementTuning.asset";

    [Test]
    public void BuiltInDefaultsAreCoherent()
    {
        Assert.That(MovementTuningComponent.Validate(MovementTuningComponent.Default, MovementTuningComponent.ReferenceSpacing),
            Is.Null);
    }

    [Test]
    public void NewConfigAssetMatchesBuiltInDefaults()
    {
        var config = ScriptableObject.CreateInstance<MovementTuningConfig>();
        try
        {
            Assert.That(config.ToComponent(), Is.EqualTo(MovementTuningComponent.Default),
                "MovementTuningConfig field defaults drifted from MovementTuningComponent.Default.");
        }
        finally { Object.DestroyImmediate(config); }
    }

    [Test]
    public void ProjectDefaultAssetIsCoherent()
    {
        var asset = AssetDatabase.LoadAssetAtPath<MovementTuningConfig>(DefaultAssetPath);
        Assert.That(asset, Is.Not.Null, $"Missing {DefaultAssetPath}.");
        Assert.That(MovementTuningComponent.Validate(asset.ToComponent(), MovementTuningComponent.ReferenceSpacing), Is.Null);
    }

    [Test]
    public void ExitRadiusMustExceedEntryRadius()
    {
        var tuning = MovementTuningComponent.Default;
        tuning.arrivalRadiusOut = tuning.arrivalRadiusIn;
        Assert.That(MovementTuningComponent.Validate(tuning, MovementTuningComponent.ReferenceSpacing), Is.Not.Null);
    }
}
