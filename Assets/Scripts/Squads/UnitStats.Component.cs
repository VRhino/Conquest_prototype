using Unity.Entities;

/// <summary>
/// Base attributes for an individual unit inside a squad.
/// Values scale with squad level in <see cref="SquadProgressionSystem"/>.
/// </summary>
public struct UnitStatsComponent : IComponentData
{
    public float vida;
    /// <summary>
    /// Final movement speed including base speed, level scaling, and weight multiplier.
    /// Calculated using UnitSpeedCalculator.CalculateFinalSpeed().
    /// </summary>
    public float velocidad;
    public float masa;
    /// <summary>
    /// Weight category of the unit: 1=light, 2=medium, 3=heavy.
    /// </summary>
    public float peso;
    public float bloqueo;

    public float defensaCortante;
    public float defensaPerforante;
    public float defensaContundente;

    public float danoCortante;
    public float danoPerforante;
    public float danoContundente;

    public float penetracionCortante;
    public float penetracionPerforante;
    public float penetracionContundente;

    public int liderazgoCosto;
}

