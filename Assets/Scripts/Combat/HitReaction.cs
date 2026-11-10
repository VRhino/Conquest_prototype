using Unity.Mathematics;

/// <summary>
/// What a hit does to the body (L1-09; ref.: the hit's reaction is decided when it is prepared, with a per-class
/// permission that an attribute can override; control immunity replaces that attribute). Values are the Animator's
/// <c>HitReactionType</c>.
/// </summary>
public enum HitReaction : byte
{
    /// <summary>None. In a hit's traits: the default reaction, a stagger.</summary>
    None = 0,
    /// <summary>Recoil: the chest goes back and the unit steps back.</summary>
    Stagger = 1,
    /// <summary>Knocked down on its back, then it gets up.</summary>
    Knockdown = 2,
    /// <summary>Thrown back through the air, then it gets up.</summary>
    Launch = 3,
    /// <summary>Only from a broken shield: the shield arm is knocked aside. Control resistance does not apply.</summary>
    GuardBreak = 4,
}

/// <summary>Pure rules of hit reactions (L1-09). The resilience break (L1-16) decides when a hit causes one.</summary>
public static class HitReactions
{
    /// <summary>Weight of a reaction: stagger 1, knock-down 2, launch 3; none and guard break 0.</summary>
    public static int Level(HitReaction reaction) => reaction switch
    {
        HitReaction.Stagger => 1,
        HitReaction.Knockdown => 2,
        HitReaction.Launch => 3,
        _ => 0,
    };

    static HitReaction FromLevel(int level) => level >= 3 ? HitReaction.Launch
        : level == 2 ? HitReaction.Knockdown
        : level == 1 ? HitReaction.Stagger
        : HitReaction.None;

    /// <summary>
    /// Reaction of a hit that breaks the target's resilience: the hit's own (none = stagger); a knock-down or a launch
    /// stays a stagger on the classes the hit is limited on; then it drops one level per whole point of the target's
    /// control resistance. At 0 there is no reaction, and no stun.
    /// </summary>
    public static HitReaction OfBreak(HitReaction requested, HitTargets limitedOn, bool targetIsHero, bool targetIsSoldier,
        float controlResistance)
    {
        var reaction = requested == HitReaction.None || requested == HitReaction.GuardBreak ? HitReaction.Stagger : requested;
        if (Level(reaction) > 1 && HitStages.Spares(limitedOn, targetIsHero, targetIsSoldier)) reaction = HitReaction.Stagger;
        return FromLevel(Level(reaction) - (int)math.floor(math.max(0f, controlResistance)));
    }

    /// <summary>Seconds the reaction keeps the target stunned (no move, attack or skill). None: 0.</summary>
    public static float StunDuration(HitReaction reaction, in ResilienceTuning tuning) => reaction switch
    {
        HitReaction.Stagger => tuning.stunDuration,
        HitReaction.Knockdown => tuning.knockdownDuration,
        HitReaction.Launch => tuning.launchDuration,
        _ => 0f,
    };

    /// <summary>
    /// Push of a heavy reaction on a soldier (distance m, duration s); 0 for the rest, which keep the push of every hit.
    /// The clips are played in place, so this push is what moves the body.
    /// </summary>
    public static float2 Push(HitReaction reaction, in ResilienceTuning tuning) => reaction switch
    {
        HitReaction.Knockdown => new float2(tuning.knockdownPushDistance, tuning.knockdownPushDuration),
        HitReaction.Launch => new float2(tuning.launchPushDistance, tuning.launchPushDuration),
        _ => float2.zero,
    };
}
