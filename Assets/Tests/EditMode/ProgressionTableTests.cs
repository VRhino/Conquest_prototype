using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Squad XP and ability unlocks from a table in data (Aprendizajes_Referencia.md L7-5).</summary>
public class ProgressionTableTests
{
    static float OldFormula(int level) => math.floor(100f * math.pow(1.1f, level - 1));

    [Test]
    public void DefaultTableGivesTheOldCurveAndUnlocks()
    {
        var data = ScriptableObject.CreateInstance<SquadProgressionData>();
        var curves = SquadProgressionCurves.Create(data);
        var missing = SquadProgressionCurves.Create(null);
        try
        {
            for (int level = 1; level < SquadProgressionCurves.MaxLevel; level++)
            {
                Assert.That(SquadProgressionCurves.XPToNext(curves, level), Is.EqualTo(OldFormula(level)), $"level {level}");
                Assert.That(SquadProgressionCurves.XPToNext(missing, level), Is.EqualTo(OldFormula(level)));
                Assert.That(SquadProgressionCurves.XPToNext(data, level), Is.EqualTo(OldFormula(level)));
            }
            Assert.That(SquadProgressionCurves.XPToNext(default(Unity.Entities.BlobAssetReference<SquadProgressionCurveBlob>), 4),
                Is.EqualTo(OldFormula(4)), "No blob: the old curve.");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(SquadProgressionCurves.UnlockLevel(curves, i), Is.EqualTo((i + 1) * 10));
                Assert.That(SquadProgressionCurves.UnlockLevel(missing, i), Is.EqualTo((i + 1) * 10));
            }
        }
        finally
        {
            curves.Dispose();
            missing.Dispose();
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void ACustomTableIsUsedAndGapsFallBack()
    {
        var data = ScriptableObject.CreateInstance<SquadProgressionData>();
        data.xpToNextLevel = new[] { 50f, 0f, 75f };
        data.abilityUnlockLevels = new[] { 3, 8 };
        var curves = SquadProgressionCurves.Create(data);
        try
        {
            Assert.That(SquadProgressionCurves.XPToNext(curves, 1), Is.EqualTo(50f));
            Assert.That(SquadProgressionCurves.XPToNext(curves, 2), Is.EqualTo(OldFormula(2)), "A 0 falls back.");
            Assert.That(SquadProgressionCurves.XPToNext(curves, 3), Is.EqualTo(75f));
            Assert.That(SquadProgressionCurves.XPToNext(curves, 10), Is.EqualTo(OldFormula(10)), "Past the table.");
            Assert.That(SquadProgressionCurves.UnlockLevel(curves, 0), Is.EqualTo(3));
            Assert.That(SquadProgressionCurves.UnlockLevel(curves, 1), Is.EqualTo(8));
            Assert.That(SquadProgressionCurves.UnlockLevel(curves, 2), Is.EqualTo(30), "Abilities past the table: every 10.");
        }
        finally
        {
            curves.Dispose();
            Object.DestroyImmediate(data);
        }
    }
}
