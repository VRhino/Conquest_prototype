using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>The rest formation («En reposo»): every troop has one, following the hero takes it, F1-F4 and X never do.</summary>
public class RestFormationTests
{
    static BlobAssetReference<FormationLibraryBlob> Library(params FormationType[] types)
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var library = ref builder.ConstructRoot<FormationLibraryBlob>();
        var formations = builder.Allocate(ref library.formations, types.Length);
        for (int i = 0; i < types.Length; i++)
            formations[i].formationType = types[i];
        return builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
    }

    static FormationType Apply(BlobAssetReference<FormationLibraryBlob> blob, FormationType current, SquadControlCommand command)
    {
        var definition = new SquadDefinitionComponent { formationLibrary = blob };
        var input = new SquadInputComponent();
        var intent = new SquadPlayerOrderIntentComponent { desiredFormation = current };
        SquadPlayerCommandResolver.TryApply(command, definition, ref input, ref intent, 0);
        return intent.desiredFormation;
    }

    static readonly SquadControlCommand Follow = new SquadControlCommand { orderIssued = true, order = SquadOrderType.FollowHero };

    [Test]
    public void FollowingTheHeroTakesTheRestFormation()
    {
        var blob = Library(FormationType.Line, FormationType.Dispersed, FormationType.Rest);
        try { Assert.That(Apply(blob, FormationType.Line, Follow), Is.EqualTo(FormationType.Rest)); }
        finally { blob.Dispose(); }
    }

    [Test]
    public void WithoutARestFormationFollowingStillOpensIntoDispersed()
    {
        var blob = Library(FormationType.Line, FormationType.Dispersed);
        try { Assert.That(Apply(blob, FormationType.Line, Follow), Is.EqualTo(FormationType.Dispersed)); }
        finally { blob.Dispose(); }
    }

    [Test]
    public void FunctionKeysAndTheCycleNeverPickTheRestFormation()
    {
        var blob = Library(FormationType.Line, FormationType.Dispersed, FormationType.Rest);
        try
        {
            Assert.That(Apply(blob, FormationType.Line, new SquadControlCommand { formationRequested = true, formationIndex = 2 }),
                Is.EqualTo(FormationType.Line), "F3 points at the rest formation, which is not one of the squad's F keys.");
            var cycle = new SquadControlCommand { formationRequested = true, cycleFormation = true };
            Assert.That(Apply(blob, FormationType.Dispersed, cycle), Is.EqualTo(FormationType.Line));
            Assert.That(Apply(blob, FormationType.Rest, cycle), Is.EqualTo(FormationType.Line), "From rest the cycle starts again.");
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void EveryTroopHasARestFormationThatHoldsAllItsSoldiers()
    {
        var squads = Resources.LoadAll<SquadData>("Squads");
        Assert.That(squads, Is.Not.Empty);
        foreach (var squad in squads)
        {
            var rest = squad.restFormation;
            Assert.That(rest, Is.Not.Null, $"{squad.name} has no rest formation.");
            Assert.That(rest.formationType, Is.EqualTo(FormationType.Rest), squad.name);
            Assert.That(rest.Validate(), Is.Null, squad.name);
            Assert.That(rest.Capacity, Is.GreaterThanOrEqualTo(squad.unitCount), squad.name);
            Assert.That(squad.formations == null || !squad.formations.Contains(rest), Is.True,
                $"{squad.name} lists its rest formation among F1-F4.");
        }
    }
}
