using Unity.Mathematics;

/// <summary>
/// Utilidad centralizada para calcular modificadores de velocidad de unidades.
/// Garantiza consistencia en todos los sistemas que necesiten aplicar estos cálculos.
/// </summary>
public static class UnitSpeedCalculator
{
    /// <summary>
    /// Calcula el multiplicador de velocidad basado en el peso de la unidad.
    /// </summary>
    /// <param name="peso">Categoría de peso: 1=ligero, 2=medio, 3=pesado</param>
    /// <returns>Multiplicador de velocidad (1.0=normal, 0.8=medio, 0.6=pesado)</returns>
    public static float GetWeightSpeedMultiplier(int peso)
    {
        return peso switch
        {
            1 => 1.0f,   // Ligero: sin penalización
            2 => 0.8f,   // Medio: -20% velocidad
            3 => 0.6f,   // Pesado: -40% velocidad
            _ => 1.0f    // Valor por defecto para casos no esperados
        };
    }

    /// <summary>
    /// Calcula la velocidad final de una unidad incluyendo velocidad base, escalado por nivel y peso.
    /// </summary>
    /// <param name="baseSpeed">Velocidad base del tipo de unidad</param>
    /// <param name="levelMultiplier">Multiplicador de escalado por nivel</param>
    /// <param name="peso">Categoría de peso de la unidad</param>
    /// <returns>Velocidad final calculada</returns>
    public static float CalculateFinalSpeed(float baseSpeed, float levelMultiplier, int peso)
    {
        float weightMultiplier = GetWeightSpeedMultiplier(peso);
        return baseSpeed * levelMultiplier * weightMultiplier;
    }
}
