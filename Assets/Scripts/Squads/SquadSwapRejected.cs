using Unity.Entities;

/// <summary>Por qué no se hizo un cambio de escuadra pedido en un punto de suministro.</summary>
public enum SquadSwapRejectReason : byte
{
    None,
    InCombat,          // la escuadra activa está en combate
    Contested,         // el punto está en disputa o ya no es del equipo
    LeftZone,          // el héroe salió del radio del punto durante la canalización
    NoReserve,         // la escuadra pedida no tiene soldados vivos
    StillRetreating,   // la escuadra pedida aún se está retirando de un cambio anterior
    NoSpawnPoint,      // el equipo no tiene punto de aparición activo
}

/// <summary>
/// El último rechazo de un cambio de escuadra en el héroe (Víctor, 18-11: avisar en pantalla). Lo escriben
/// SquadSwapSystem, SquadSwapChannelingSystem y SquadSwapExecutionSystem; la interfaz lo lee y avisa cuando
/// <see cref="count"/> cambia, así nadie tiene que consumirlo ni quitarlo.
/// </summary>
public struct SquadSwapRejectedComponent : IComponentData
{
    public SquadSwapRejectReason reason;
    /// <summary>Rechazos acumulados en la batalla: sube en uno con cada rechazo.</summary>
    public int count;

    /// <summary>Apunta un rechazo con el ECB del sistema (sin cambios estructurales dentro de su foreach).</summary>
    public static void Record(EntityManager em, EntityCommandBuffer ecb, Entity hero, SquadSwapRejectReason reason)
    {
        int n = em.HasComponent<SquadSwapRejectedComponent>(hero) ? em.GetComponentData<SquadSwapRejectedComponent>(hero).count + 1 : 1;
        ecb.AddComponent(hero, new SquadSwapRejectedComponent { reason = reason, count = n });
    }
}
