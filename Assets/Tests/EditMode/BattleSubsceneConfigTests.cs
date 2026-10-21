using System.IO;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// The battle subscenes must bake every config singleton their systems require. HeroGameplayConfigAuthoring was only
/// in SettlementHeroWorld, so in battle HeroAttackSystem and HeroStaminaSystem never ran (RequireForUpdate).
/// </summary>
public class BattleSubsceneConfigTests
{
    static readonly string[] BattleSubscenes =
    {
        "Assets/Scenes/BattleTestScene/DotsWorld.unity",
        "Assets/Scenes/GameScenes/BattleScene/DOTSWorld.unity",
    };

    [TestCase(typeof(HeroGameplayConfigAuthoring))]
    [TestCase(typeof(SquadSpawnConfigAuthoring))]
    public void EveryBattleSubsceneBakesTheConfig(System.Type authoring)
    {
        // Script files are named <Subject>.Authoring.cs, not after the class: look the script up by its class.
        string scriptPath = null;
        foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            if (script != null && script.GetClass() == authoring) { scriptPath = AssetDatabase.GetAssetPath(script); break; }
        Assert.That(scriptPath, Is.Not.Null, authoring.Name);
        string guid = AssetDatabase.AssetPathToGUID(scriptPath);
        foreach (string scene in BattleSubscenes)
        {
            Assert.That(File.Exists(scene), Is.True, scene);
            Assert.That(File.ReadAllText(scene), Does.Contain($"guid: {guid}"),
                $"{scene} has no {authoring.Name}: the systems that require its singleton never run in battle.");
        }
    }
}
