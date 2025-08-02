using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de datos de todos los tipos de escuadrones (SquadData) disponibles en el juego.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Squad Database")]
public class SquadDatabase : ScriptableObject
{
    public List<SquadData> allSquads;
}
