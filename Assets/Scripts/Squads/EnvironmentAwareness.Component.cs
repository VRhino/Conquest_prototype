using Unity.Entities;

/// <summary>
/// Describes the immediate environment around a squad.
/// Updated by sensors to allow formation adaptation.
/// </summary>
public struct EnvironmentAwarenessComponent : IComponentData
{
    /// <summary>Radius used for obstacle detection checks.</summary>
    public float detectionRadius;

    /// <summary>Current type of terrain the squad is navigating.</summary>
    public TerrainType terrainType;

    /// <summary>True if obstacles were detected near the squad.</summary>
    public bool obstacleDetected;
    
    /// <summary>True if environmental conditions require individual unit navigation adaptation.</summary>
    public bool requiresAdaptation;
}
