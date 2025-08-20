using UnityEngine;

/// <summary>
/// Calculadora para el sistema de experiencia y progreso de nivel del héroe.
/// Maneja cálculos de XP, niveles, progreso y requisitos para subir de nivel.
/// </summary>
public static class HeroExperienceCalculator
{
    /// <summary>
    /// Constantes para el sistema de experiencia.
    /// </summary>
    private static class ExperienceConstants
    {
        public const int BASE_XP_PER_LEVEL = 100;
        public const float XP_GROWTH_FACTOR = 1.2f; // Cada nivel requiere 20% más XP que el anterior
        public const int MAX_LEVEL = 50; // Nivel máximo del héroe
        public const int MIN_LEVEL = 1; // Nivel mínimo del héroe
    }

    /// <summary>
    /// Calcula el nivel actual basado en la experiencia total acumulada.
    /// </summary>
    /// <param name="currentXP">Experiencia total acumulada</param>
    /// <returns>Nivel actual del héroe</returns>
    public static int GetCurrentLevel(int currentXP)
    {
        if (currentXP <= 0)
            return ExperienceConstants.MIN_LEVEL;

        int level = ExperienceConstants.MIN_LEVEL;
        int totalXPRequired = 0;

        while (level < ExperienceConstants.MAX_LEVEL)
        {
            int xpForNextLevel = GetXPRequiredForLevel(level + 1);
            if (totalXPRequired + xpForNextLevel > currentXP)
                break;
            
            totalXPRequired += xpForNextLevel;
            level++;
        }

        return level;
    }

    /// <summary>
    /// Obtiene la experiencia requerida para alcanzar un nivel específico.
    /// </summary>
    /// <param name="level">Nivel objetivo</param>
    /// <returns>XP requerida para ese nivel específico (no acumulada)</returns>
    public static int GetXPRequiredForLevel(int level)
    {
        if (level <= ExperienceConstants.MIN_LEVEL)
            return 0;

        // Fórmula exponencial: cada nivel requiere más XP que el anterior
        float xpRequired = ExperienceConstants.BASE_XP_PER_LEVEL * 
                          Mathf.Pow(ExperienceConstants.XP_GROWTH_FACTOR, level - 2);
        
        return Mathf.RoundToInt(xpRequired);
    }

    /// <summary>
    /// Obtiene la experiencia total requerida para alcanzar un nivel específico.
    /// </summary>
    /// <param name="level">Nivel objetivo</param>
    /// <returns>XP total acumulada necesaria para ese nivel</returns>
    public static int GetTotalXPForLevel(int level)
    {
        if (level <= ExperienceConstants.MIN_LEVEL)
            return 0;

        int totalXP = 0;
        for (int i = ExperienceConstants.MIN_LEVEL; i < level; i++)
        {
            totalXP += GetXPRequiredForLevel(i + 1);
        }

        return totalXP;
    }

    /// <summary>
    /// Obtiene la experiencia necesaria para subir al siguiente nivel.
    /// </summary>
    /// <param name="currentLevel">Nivel actual del héroe</param>
    /// <returns>XP necesaria para el siguiente nivel</returns>
    public static int GetXPForNextLevel(int currentLevel)
    {
        if (currentLevel >= ExperienceConstants.MAX_LEVEL)
            return 0; // Ya está en nivel máximo

        return GetXPRequiredForLevel(currentLevel + 1);
    }

    /// <summary>
    /// Calcula el progreso hacia el siguiente nivel como porcentaje (0.0 - 1.0).
    /// </summary>
    /// <param name="currentXP">Experiencia total acumulada</param>
    /// <param name="currentLevel">Nivel actual del héroe</param>
    /// <returns>Progreso hacia el siguiente nivel (0.0 = inicio del nivel actual, 1.0 = listo para subir)</returns>
    public static float GetLevelProgress(int currentXP, int currentLevel)
    {
        if (currentLevel >= ExperienceConstants.MAX_LEVEL)
            return 1.0f; // Nivel máximo alcanzado

        int xpAtCurrentLevel = GetTotalXPForLevel(currentLevel);
        int xpNeededForNext = GetXPForNextLevel(currentLevel);
        int currentLevelXP = currentXP - xpAtCurrentLevel;

        if (xpNeededForNext <= 0)
            return 1.0f;

        return Mathf.Clamp01((float)currentLevelXP / xpNeededForNext);
    }

    /// <summary>
    /// Obtiene información detallada del progreso de experiencia del héroe.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <returns>Estructura con información detallada del progreso</returns>
    public static ExperienceInfo GetExperienceInfo(HeroData heroData)
    {
        if (heroData == null)
        {
            Debug.LogWarning("[HeroExperienceCalculator] HeroData es null.");
            return new ExperienceInfo();
        }

        int currentLevel = GetCurrentLevel(heroData.currentXP);
        int xpForNextLevel = GetXPForNextLevel(currentLevel);
        float progress = GetLevelProgress(heroData.currentXP, currentLevel);
        int xpAtCurrentLevel = GetTotalXPForLevel(currentLevel);
        int currentLevelXP = heroData.currentXP - xpAtCurrentLevel;

        return new ExperienceInfo
        {
            currentXP = heroData.currentXP,
            currentLevel = currentLevel,
            xpInCurrentLevel = currentLevelXP,
            xpNeededForNextLevel = xpForNextLevel,
            progressToNextLevel = progress,
            isMaxLevel = currentLevel >= ExperienceConstants.MAX_LEVEL
        };
    }

    /// <summary>
    /// Estructura que contiene información detallada sobre el progreso de experiencia.
    /// </summary>
    public struct ExperienceInfo
    {
        public int currentXP;
        public int currentLevel;
        public int xpInCurrentLevel;
        public int xpNeededForNextLevel;
        public float progressToNextLevel;
        public bool isMaxLevel;

        /// <summary>
        /// Texto formateado para mostrar progreso de XP.
        /// Ejemplo: "250 / 400 XP"
        /// </summary>
        public string GetProgressText()
        {
            if (isMaxLevel)
                return "MAX LEVEL";
            
            return $"{xpInCurrentLevel} / {xpNeededForNextLevel} XP";
        }
    }
}
