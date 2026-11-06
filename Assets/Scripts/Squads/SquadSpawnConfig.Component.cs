using Unity.Entities;

public struct SquadSpawnConfigComponent : IComponentData
{
    public bool enableEnvironmentAdaptation;
    public float ownerDeathRetreatDuration;
    public float retreatArrivalThreshold;
    public float anchorMovingSpeedThreshold;
    public float animationMovingSpeedThreshold;
    public float animationRunningSpeedRatio;
    public float bodyblockRadius;
    public float bodyblockOverlapEpsilonSq;
    public int bodyblockJobBatchSize;
    public int bodyblockParallelThreshold;
    public float bodyblockRepulsionStrength;
    public float bodyblockWallStrength;
    public float bodyblockMaxPushSpeed;
    public bool  bodyblockResolveOverlapOnly; // Capsule-like collision instead of the spring push.
    public float bodyblockEngagingRadius;
    public float bodyblockEngagingStrength;
    public float navMeshDestinationSampleRadius;
    public float navMeshDestinationChangeThreshold;
    public float navMeshFailureRetryDistance;
    public float navMeshFailureRetryInterval;
    public float heroAIArrivalDistance;
    public float heroAIThinkInterval;    // s between thinks of one AI hero (L4-01)
    public int   heroAIMaxThinksPerFrame;
    public float heroAIUnderAttackOpenTime;  // s the raw condition must hold to open (L4-02)
    public float heroAIUnderAttackCloseTime; // s it must be false to close
    public HeroAISkillTuning heroAISkills;   // AI use of skills and choice of an ally (H1, L4-08)
    public float squadSpawnOffset;    // Distancia delante del héroe al spawnear el squad
    public float unitRotationSpeed;   // Velocidad de rotación inicial de las unidades
    public float heroSlotSpacing;     // Espacio lateral entre slots de héroe (metros)
    public float followForwardOffset; // Distancia delante del héroe al formar en Follow/Attack
    public float unitLeashDistance;   // Max distance (from formation slot) a unit will chase an enemy
    public float minCombatDuration;   // Segundos mínimos en InCombat tras perder enemigos de rango
    public LineOfSightTuning lineOfSight; // Rays that decide what a squad sees (L4-03)
    public FormationReadinessTuning formationReadiness; // When a formation is ready and how long the AI waits for it (F12)
    public HitDirectionTuning hitDirection; // Flank, back and ambush hits (L1-15)
    public ResilienceTuning resilience;     // Wear, recovery and stun of resilience (L1-16)
    public SoldierOrderTuning soldierOrders; // How a squad ability run per soldier is spread (L2-17)
}
