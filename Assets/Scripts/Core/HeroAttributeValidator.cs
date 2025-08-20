using UnityEngine;

/// <summary>
/// Servicio de validación para modificaciones de atributos del héroe.
/// Maneja límites de clase, restricciones de puntos disponibles y validaciones específicas.
/// </summary>
public static class HeroAttributeValidator
{
    /// <summary>
    /// Límites por defecto para atributos básicos (pueden variar según clase en futuras implementaciones).
    /// </summary>
    private static class DefaultLimits
    {
        public const int MIN_ATTRIBUTE_VALUE = 0;
        public const int MAX_ATTRIBUTE_VALUE = 100; // Ajustable según balance del juego
        public const int MIN_LEADERSHIP = 700; // Base fija no modificable directamente
    }

    /// <summary>
    /// Valida si se puede modificar un atributo específico del héroe.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo a modificar</param>
    /// <param name="newValue">Nuevo valor propuesto</param>
    /// <param name="pointsRequired">Puntos de atributo requeridos para el cambio</param>
    /// <returns>True si la modificación es válida, False si no</returns>
    public static bool CanModifyAttribute(HeroData heroData, string attributeName, float newValue, int pointsRequired = 1)
    {
        if (heroData == null || string.IsNullOrEmpty(attributeName))
        {
            Debug.LogWarning("[HeroAttributeValidator] HeroData o attributeName es inválido.");
            return false;
        }

        // Validar puntos disponibles
        if (!HasEnoughPoints(heroData, pointsRequired))
        {
            Debug.LogWarning($"[HeroAttributeValidator] Hero {heroData.heroName} no tiene suficientes puntos de atributo. Disponibles: {heroData.attributePoints}, Requeridos: {pointsRequired}");
            return false;
        }

        // Validar límites del atributo
        var limits = GetAttributeLimits(heroData, attributeName);
        if (newValue < limits.min || newValue > limits.max)
        {
            Debug.LogWarning($"[HeroAttributeValidator] Valor {newValue} para {attributeName} está fuera de límites [{limits.min}, {limits.max}]");
            return false;
        }

        // Validaciones específicas por atributo
        return ValidateSpecificAttribute(heroData, attributeName, newValue);
    }

    /// <summary>
    /// Obtiene los límites mínimo y máximo para un atributo específico.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>Tupla con valores mínimo y máximo</returns>
    public static (float min, float max) GetAttributeLimits(HeroData heroData, string attributeName)
    {
        if (heroData == null || string.IsNullOrEmpty(attributeName))
            return (0f, 0f);

        switch (attributeName.ToLower())
        {
            case "fuerza":
            case "strength":
            case "destreza":
            case "dexterity":
            case "armadura":
            case "armor":
            case "vitalidad":
            case "vitality":
                return GetBasicAttributeLimits(heroData, attributeName);
            
            case "liderazgo":
            case "leadership":
                return GetLeadershipLimits(heroData);
            
            default:
                Debug.LogWarning($"[HeroAttributeValidator] Atributo no reconocido: {attributeName}");
                return (DefaultLimits.MIN_ATTRIBUTE_VALUE, DefaultLimits.MAX_ATTRIBUTE_VALUE);
        }
    }

    /// <summary>
    /// Verifica si el héroe tiene suficientes puntos de atributo disponibles.
    /// Considera tanto los puntos base como los ya usados en modificaciones temporales.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="requiredPoints">Puntos requeridos</param>
    /// <returns>True si tiene suficientes puntos, False si no</returns>
    public static bool HasEnoughPoints(HeroData heroData, int requiredPoints)
    {
        if (heroData == null)
            return false;

        // Solo validamos puntos positivos (incrementos)
        if (requiredPoints <= 0)
            return true;

        // Obtener puntos disponibles considerando cambios temporales
        string heroId = GetHeroId(heroData);
        int availablePoints = HeroTempAttributeService.GetAvailablePoints(heroId, heroData);
        
        bool hasEnough = availablePoints >= requiredPoints;
        
        // DEBUG: Log para diagnosticar
        Debug.Log($"[HasEnoughPoints] heroId={heroId}, available={availablePoints}, required={requiredPoints}, hasEnough={hasEnough}");
        
        return hasEnough;
    }

    /// <summary>
    /// Obtiene el ID único del héroe para identificarlo en los sistemas de cache.
    /// Simplificado para usar solo heroName ya que los nombres son únicos globalmente.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <returns>ID único del héroe</returns>
    public static string GetHeroId(HeroData heroData)
    {
        if (heroData == null)
            return string.Empty;

        // Los nombres de héroe son únicos globalmente, no necesitamos ID compuesto
        return heroData.heroName;
    }

    /// <summary>
    /// Valida si se puede incrementar un atributo en 1 punto.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>True si se puede incrementar, False si no</returns>
    public static bool CanIncrementAttribute(HeroData heroData, string attributeName)
    {
        if (heroData == null || string.IsNullOrEmpty(attributeName))
            return false;

        // Obtener valor actual considerando cambios temporales
        string heroId = GetHeroId(heroData);
        float baseValue = GetCurrentAttributeValue(heroData, attributeName);
        float currentValue = HeroTempAttributeService.GetTempAttributeValue(heroId, attributeName, baseValue);
        float newValue = currentValue + 1f;

        bool canIncrement = CanModifyAttribute(heroData, attributeName, newValue, 1);
        
        return canIncrement;
    }

    /// <summary>
    /// Valida si se puede decrementar un atributo en 1 punto.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>True si se puede decrementar, False si no</returns>
    public static bool CanDecrementAttribute(HeroData heroData, string attributeName)
    {
        if (heroData == null || string.IsNullOrEmpty(attributeName))
            return false;

        // Obtener valor actual considerando cambios temporales
        string heroId = GetHeroId(heroData);
        float baseValue = GetCurrentAttributeValue(heroData, attributeName);
        float currentValue = HeroTempAttributeService.GetTempAttributeValue(heroId, attributeName, baseValue);
        float newValue = currentValue - 1f;

        // Para decrementar, no necesitamos puntos adicionales, pero sí validar límites
        var limits = GetAttributeLimits(heroData, attributeName);
        return newValue >= limits.min;
    }

    /// <summary>
    /// Obtiene el valor actual de un atributo específico del héroe.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>Valor actual del atributo</returns>
    public static float GetCurrentAttributeValue(HeroData heroData, string attributeName)
    {
        if (heroData == null || string.IsNullOrEmpty(attributeName))
            return 0f;

        switch (attributeName.ToLower())
        {
            case "fuerza":
            case "strength":
                return heroData.fuerza;
            case "destreza":
            case "dexterity":
                return heroData.destreza;
            case "armadura":
            case "armor":
                return heroData.armadura;
            case "vitalidad":
            case "vitality":
                return heroData.vitalidad;
            case "liderazgo":
            case "leadership":
                // El liderazgo se calcula, no se modifica directamente
                return HeroLeadershipCalculator.CalculateLeadership(heroData);
            default:
                Debug.LogWarning($"[HeroAttributeValidator] Atributo no reconocido: {attributeName}");
                return 0f;
        }
    }

    /// <summary>
    /// Obtiene los límites para atributos básicos (fuerza, destreza, etc.).
    /// </summary>
    private static (float min, float max) GetBasicAttributeLimits(HeroData heroData, string attributeName)
    {
        // TODO: Implementar límites específicos por clase cuando esté disponible el sistema de clases completo
        // Por ahora usamos límites por defecto
        return (DefaultLimits.MIN_ATTRIBUTE_VALUE, DefaultLimits.MAX_ATTRIBUTE_VALUE);
    }

    /// <summary>
    /// Obtiene los límites para liderazgo (calculado, no modificable directamente).
    /// </summary>
    private static (float min, float max) GetLeadershipLimits(HeroData heroData)
    {
        // El liderazgo no se modifica directamente, se calcula basado en equipamiento
        float currentLeadership = HeroLeadershipCalculator.CalculateLeadership(heroData);
        return (currentLeadership, currentLeadership);
    }

    /// <summary>
    /// Validaciones específicas por tipo de atributo.
    /// </summary>
    private static bool ValidateSpecificAttribute(HeroData heroData, string attributeName, float newValue)
    {
        switch (attributeName.ToLower())
        {
            case "liderazgo":
            case "leadership":
                // El liderazgo no se puede modificar directamente
                Debug.LogWarning("[HeroAttributeValidator] El liderazgo no se puede modificar directamente. Se calcula basado en equipamiento.");
                return false;
            
            default:
                return true; // Otros atributos son modificables
        }
    }
}
