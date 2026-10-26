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

    /// <summary>F15: the arrival radii of every formation asset keep r_in &lt; s/4 and r_out ≤ s/2 at its own spacing.</summary>
    [Test]
    public void ArrivalRadiiFitEveryFormationSpacing()
    {
        var tuning = AssetDatabase.LoadAssetAtPath<MovementTuningConfig>(DefaultAssetPath).ToComponent();
        string[] guids = AssetDatabase.FindAssets("t:FormationDefinition");
        Assert.That(guids, Is.Not.Empty);
        foreach (string guid in guids)
        {
            var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            float s = formation.EffectiveSpacing;
            var radii = FormationArrivalRadii.From(tuning, s);
            Assert.That(Mathf.Sqrt(radii.inSq), Is.LessThan(0.25f * s), formation.name);
            Assert.That(Mathf.Sqrt(radii.outSq), Is.LessThanOrEqualTo(0.5f * s + 1e-5f), formation.name);
            Assert.That(radii.inSq, Is.LessThan(radii.outSq), formation.name);
        }
    }

    [Test]
    public void WideFormationsKeepTheTunedArrivalRadii()
    {
        var tuning = MovementTuningComponent.Default;
        var radii = FormationArrivalRadii.From(tuning, 2f * MovementTuningComponent.ReferenceSpacing);
        Assert.That(radii.inSq, Is.EqualTo(tuning.arrivalRadiusIn * tuning.arrivalRadiusIn).Within(1e-6f));
        Assert.That(radii.outSq, Is.EqualTo(tuning.arrivalRadiusOut * tuning.arrivalRadiusOut).Within(1e-6f));
        Assert.That(FormationArrivalRadii.ArrivalRadiusIn(tuning, 0.5f), Is.EqualTo(0.5f * tuning.arrivalRadiusIn).Within(1e-6f));
    }

    [Test]
    public void ExitRadiusMustExceedEntryRadius()
    {
        var tuning = MovementTuningComponent.Default;
        tuning.arrivalRadiusOut = tuning.arrivalRadiusIn;
        Assert.That(MovementTuningComponent.Validate(tuning, MovementTuningComponent.ReferenceSpacing), Is.Not.Null);
    }
}
