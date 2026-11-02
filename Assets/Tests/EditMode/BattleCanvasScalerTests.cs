using System.IO;
using NUnit.Framework;

/// <summary>
/// Every battle canvas must scale the same way. The HUD is duplicated in both battle scenes and SkillBar is a canvas of
/// its own: BattleTestScene stayed in Constant Pixel Size, so below 1920 wide the skill bar slid under the squad panel.
/// Expand keeps the logical canvas at 1920x1080 or larger, so what is laid out for that size always fits.
/// </summary>
public class BattleCanvasScalerTests
{
    [TestCase("Assets/Scenes/GameScenes/BattleScene.unity")]
    [TestCase("Assets/Scenes/BattleTestScene.unity")]
    [TestCase("Assets/Resources/UI/SkillBar.prefab")]
    [TestCase("Assets/Resources/UI/PerkSelectionPanel.prefab")]
    public void BattleCanvasScalesWithScreenSizeInExpandMode(string path)
    {
        Assert.That(File.Exists(path), Is.True, path);
        string yaml = File.ReadAllText(path);
        Assert.That(yaml, Does.Contain("m_UiScaleMode: 1"), path);
        Assert.That(yaml, Does.Not.Contain("m_UiScaleMode: 0"), $"{path} keeps a Constant Pixel Size canvas.");
        Assert.That(yaml, Does.Contain("m_ReferenceResolution: {x: 1920, y: 1080}"), path);
        Assert.That(yaml, Does.Contain("m_ScreenMatchMode: 1"), path);
    }
}
