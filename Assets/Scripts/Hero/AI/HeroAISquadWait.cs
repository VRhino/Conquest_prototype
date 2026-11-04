using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// F12: an AI hero does not order its squad to charge while the formation is not ready. It stands still with the squad
/// on <see cref="SquadOrderType.FollowHero"/> so the stragglers reach their slots, then charges. It never waits when the
/// squad already fights, when an enemy hero is right on it, or for longer than the tuned maximum in one charge.
/// Applied by the behaviors (Balanced, Rusher) to their decision before publishing it.
/// </summary>
public static class HeroAISquadWait
{
    /// <summary>True when the decision was turned into a wait for the squad.</summary>
    public static bool Hold(ref HeroAIDecision dec, HeroAIBlackboard bb, double now, in FormationReadinessTuning tuning)
    {
        bool charge = dec.hasNewSquadOrder && dec.squadOrder == SquadOrderType.Attack;
        if (!charge)
        {
            bb.waitingForSquad = false; // The next charge gets its own wait.
            return false;
        }

        bool enemyOnUs = bb.nearestEnemyHero != Entity.Null
                         && bb.nearestEnemyDistanceSq <= math.square(tuning.heroWaitEnemyDistance);
        if (!bb.hasSquad || bb.squadIsInCombat || bb.squadFormationReady || enemyOnUs) return false;

        if (!bb.waitingForSquad)
        {
            bb.waitingForSquad = true;
            bb.waitForSquadSince = now;
        }
        if (now - bb.waitForSquadSince >= tuning.heroMaxWait) return false; // Waited enough: charge as it is.

        dec.action         = AIActionType.Idle;
        dec.targetEntity   = Entity.Null;
        dec.targetPosition = bb.selfPosition;
        dec.shouldAttack   = false;
        dec.shouldSprint   = false;
        dec.squadOrder     = SquadOrderType.FollowHero;
        return true;
    }
}
