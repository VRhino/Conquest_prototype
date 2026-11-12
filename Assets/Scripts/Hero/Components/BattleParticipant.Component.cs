using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Which participant of the battle this hero entity is: the <c>heroId</c> of its <see cref="BattleHeroData"/>. The battle
/// server uses it to give each player the hero of its token and to report each hero and squad of the ticket by id.
/// </summary>
public struct BattleParticipantComponent : IComponentData
{
    public FixedString64Bytes heroId;
}
