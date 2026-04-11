using Unity.Entities;

public struct ShieldConfigComponent : IComponentData
{
    public float defaultMaxBlock;           // maxBlock cuando el prefab no especifica override
    public float defaultRegenRate;          // regenRate cuando el prefab no especifica override
    public float defaultBreakStunDuration;  // segundos de stun al romper el escudo (default para héroes / sin SquadData)
    public float forwardBlockDotThreshold;  // dot mínimo (attackDir · -targetFwd) para considerar golpe frontal
}
