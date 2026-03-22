using Unity.Entities;

public struct SquadSpawnConfigComponent : IComponentData
{
    public float squadSpawnOffset;    // Distancia delante del héroe al spawnear el squad
    public float unitMinDistance;     // Distancia mínima entre unidades (spacing)
    public float unitRepelForce;      // Fuerza de repulsión entre unidades
    public float unitRotationSpeed;   // Velocidad de rotación inicial de las unidades
    public float heroSlotSpacing;     // Espacio lateral entre slots de héroe (metros)
    public float followForwardOffset; // Distancia delante del héroe al formar en Follow/Attack
    public float unitLeashDistance;   // Max distance (from formation slot) a unit will chase an enemy
    public int   maxUnitsPerTarget;   // Max units allowed to target the same enemy
    public float unitMoveDelayMin;    // Delay mínimo antes de moverse (orden de formación)
    public float unitMoveDelayMax;    // Delay máximo antes de moverse (orden de formación)
    public float unitFollowDelayMin;  // Delay mínimo antes de moverse (follow héroe)
    public float unitFollowDelayMax;  // Delay máximo antes de moverse (follow héroe)
}
