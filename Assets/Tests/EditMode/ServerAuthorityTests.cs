using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// The systems that decide the match are marked <see cref="ServerAuthoritativeAttribute"/> and stay free of input and
/// presentation (Aprendizajes_Referencia.md L4-06).
/// </summary>
public class ServerAuthorityTests
{
    /// <summary>Visibility, detection, squad and hero AI, order resolution and targeting.</summary>
    static readonly Type[] Deciders =
    {
        typeof(EnemyDetectionSystem), typeof(LineOfSightSystem), typeof(SquadAISystem), typeof(CombatReactionSystem),
        typeof(OrderResolutionSystem), typeof(UnitTargetingSystem), typeof(BattleWorldStateSystem),
        typeof(HeroAIPerceptionSystem), typeof(HeroAIRusherSystem), typeof(HeroAIBalancedSystem),
        typeof(HeroAISkillSystem), typeof(HeroAIExecutionSystem),
    };

    /// <summary>Input, camera, animation, scene lookups, floating text and unseeded or frame-count randomness.</summary>
    static readonly Regex Forbidden = new Regex(
        @"Keyboard\.current|Mouse\.current|UnityEngine\.Input\b|\bInput\.Get|Camera\.main|\bAnimator\b|FindObjectOfType|" +
        @"FindAnyObjectByType|FindObjectsOfType|UnityEngine\.Random|\bRandom\.(value|Range)|Time\.frameCount|" +
        @"FloatingCombatText|HitImpactEffectManager|EntityVisualSync");

    [Test]
    public void EveryDecidingSystemIsMarked()
    {
        var unmarked = Deciders.Where(t => !t.IsDefined(typeof(ServerAuthoritativeAttribute), false)).Select(t => t.Name).ToArray();
        Assert.That(unmarked, Is.Empty, "Deciding systems without [ServerAuthoritative].");
    }

    [Test]
    public void MarkedSystemsReadNoInputNorPresentation()
    {
        var marked = typeof(ServerAuthoritativeAttribute).Assembly.GetTypes()
            .Where(t => t.IsDefined(typeof(ServerAuthoritativeAttribute), false)).ToArray();
        Assert.That(marked.Length, Is.GreaterThanOrEqualTo(Deciders.Length));
        var sources = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories);
        foreach (var type in marked)
        {
            var declaration = new Regex(@"\b(class|struct)\s+" + type.Name + @"\b");
            var files = sources.Where(f => declaration.IsMatch(File.ReadAllText(f))).ToArray();
            Assert.That(files, Is.Not.Empty, $"Source of {type.Name} not found.");
            foreach (var file in files)
            {
                var match = Forbidden.Match(File.ReadAllText(file));
                Assert.That(match.Success, Is.False, $"{type.Name} ({file}) uses {match.Value}.");
            }
        }
    }
}
