using System.Collections.Generic;

/// <summary>
/// Servicio centralizado para la manipulación y creación de datos de héroe (HeroData).
/// Encapsula las reglas de negocio iniciales (estadísticas base, economía inicial, escuadrones).
/// </summary>
public static class HeroDataService
{
    /// <summary>
    /// Crea una nueva instancia de HeroData con los valores y recursos iniciales por defecto.
    /// Inyecta automáticamente el progreso de los escuadrones iniciales permitidos.
    /// </summary>
    /// <param name="heroName">Nombre del héroe proporcionado por el jugador.</param>
    /// <param name="avatar">Configuración de partes visuales del avatar.</param>
    /// <param name="classId">ID de la clase seleccionada (ej. SwordAndShield).</param>
    /// <param name="gender">Género seleccionado en string.</param>
    /// <param name="equipment">Equipamiento inicial asignado por la clase.</param>
    /// <returns>Una nueva instancia de <see cref="HeroData"/> configurada y lista para usarse.</returns>
    public static HeroData CreateNewHero(string heroName, AvatarParts avatar, string classId, string gender, Equipment equipment)
    {
        HeroData hero = new HeroData
        {
            heroName = heroName,
            avatar = avatar,
            classId = classId,
            gender = gender,
            level = 1,
            currentXP = 0,
            attributePoints = 0,
            perkPoints = 0,
            bronze = 500, // Bono inicial
            equipment = equipment,
            availableSquads = new List<string> { "sqd01", "arc01", "spm01" }
        };

        // Crear instancias iniciales de escuadrón para el nuevo héroe usando el servicio de datos
        foreach (string squadId in hero.availableSquads)
        {
            var squadInstance = SquadDataService.CreateSquadInstance(squadId);
            if (squadInstance != null)
            {
                hero.squadProgress.Add(squadInstance);
            }
        }

        return hero;
    }
}
