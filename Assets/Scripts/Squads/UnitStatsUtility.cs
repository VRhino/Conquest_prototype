using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Utilidad centralizada para aplicar escalado de stats a unidades de escuadrón.
/// Evita la duplicación de lógica entre SquadProgressionSystem y UnitStatScalingSystem.
/// </summary>
public static class UnitStatsUtility
{
    /// <summary>
    /// Aplica stats escalados por nivel a todas las unidades de un escuadrón.
    /// </summary>
    /// <param name="squadEntity">Entidad del escuadrón</param>
    /// <param name="data">Datos base del escuadrón</param>
    /// <param name="level">Nivel actual del escuadrón</param>
    /// <param name="entityManager">EntityManager para acceso a componentes</param>
    /// <param name="unitBufferLookup">Lookup para buffers de unidades</param>
    public static void ApplyStatsToSquad(
        Entity squadEntity,
        SquadDataComponent data,
        int level,
        EntityManager entityManager,
        BufferLookup<SquadUnitElement> unitBufferLookup)
    {
        if (!unitBufferLookup.HasBuffer(squadEntity))
            return;

        // Calcular multiplicadores basados en nivel
        int index = math.clamp(level - 1, 0, data.curves.Value.vida.Length - 1);
        float vidaMul = data.curves.Value.vida[index];
        float danoMul = data.curves.Value.dano[index];
        float defMul = data.curves.Value.defensa[index];
        float velMul = data.curves.Value.velocidad[index];

        // Aplicar stats a cada unidad
        DynamicBuffer<SquadUnitElement> units = unitBufferLookup[squadEntity];
        foreach (var unitElement in units)
        {
            if (!entityManager.Exists(unitElement.Value))
                continue;

            ApplyStatsToUnit(unitElement.Value, data, vidaMul, danoMul, defMul, velMul, entityManager);
        }
    }

    /// <summary>
    /// Aplica stats escalados a una unidad individual.
    /// </summary>
    /// <param name="unitEntity">Entidad de la unidad</param>
    /// <param name="data">Datos base del escuadrón</param>
    /// <param name="vidaMul">Multiplicador de vida</param>
    /// <param name="danoMul">Multiplicador de daño</param>
    /// <param name="defMul">Multiplicador de defensa</param>
    /// <param name="velMul">Multiplicador de velocidad</param>
    /// <param name="entityManager">EntityManager para acceso a componentes</param>
    public static void ApplyStatsToUnit(
        Entity unitEntity,
        SquadDataComponent data,
        float vidaMul,
        float danoMul,
        float defMul,
        float velMul,
        EntityManager entityManager)
    {
        // Calcular velocidad final usando la utilidad centralizada
        int peso = (int)math.round(data.peso);
        float finalSpeed = UnitSpeedCalculator.CalculateFinalSpeed(data.velocidadBase, velMul, peso);

        // Crear componente de stats principal
        var stats = new UnitStatsComponent
        {
            vida = data.vidaBase * vidaMul,
            velocidad = finalSpeed,
            masa = data.masa,
            peso = peso,
            bloqueo = data.bloqueo,
            defensaCortante = data.defensaCortante * defMul,
            defensaPerforante = data.defensaPerforante * defMul,
            defensaContundente = data.defensaContundente * defMul,
            danoCortante = data.danoCortante * danoMul,
            danoPerforante = data.danoPerforante * danoMul,
            danoContundente = data.danoContundente * danoMul,
            penetracionCortante = data.penetracionCortante,
            penetracionPerforante = data.penetracionPerforante,
            penetracionContundente = data.penetracionContundente,
            liderazgoCosto = data.liderazgoCost
        };

        // Aplicar stats principales
        if (entityManager.HasComponent<UnitStatsComponent>(unitEntity))
            entityManager.SetComponentData(unitEntity, stats);
        else
            entityManager.AddComponentData(unitEntity, stats);

        // Aplicar stats de unidad a distancia si corresponde
        if (data.esUnidadADistancia)
        {
            var rangedStats = new UnitRangedStatsComponent
            {
                alcance = data.alcance,
                precision = data.precision,
                cadenciaFuego = data.cadenciaFuego,
                velocidadRecarga = data.velocidadRecarga,
                municionTotal = data.municionTotal
            };

            if (entityManager.HasComponent<UnitRangedStatsComponent>(unitEntity))
                entityManager.SetComponentData(unitEntity, rangedStats);
            else
                entityManager.AddComponentData(unitEntity, rangedStats);
        }
        else if (entityManager.HasComponent<UnitRangedStatsComponent>(unitEntity))
        {
            // Remover stats de distancia si la unidad ya no es a distancia
            entityManager.RemoveComponent<UnitRangedStatsComponent>(unitEntity);
        }
    }
}
