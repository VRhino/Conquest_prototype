using UnityEngine;

/// <summary>
/// Configuration data for a controllable squad.
/// </summary>
[CreateAssetMenu(fileName = "SquadData", menuName = "Game/Squad Data")]
public class SquadData : ScriptableObject
{
    /// <summary>Unique identifier for the squad.</summary>
    public int squadID;

    /// <summary>Display name shown in the UI.</summary>
    public string squadName;

    /// <summary>Icon representing the squad.</summary>
    public Sprite icon;

    /// <summary>Detailed description of the squad.</summary>
    public string description;

    /// <summary>Leadership cost required to deploy the squad.</summary>
    public float leadershipCost;

    /// <summary>Prefab that spawns the squad units in scene.</summary>
    public GameObject squadPrefab;

    /// <summary>Default formation used when spawning the squad.</summary>
    public FormationType defaultFormation;

    /// <summary>Hero level required to unlock the squad.</summary>
    public int unlockLevel;

    /// <summary>Whether the squad is available from the beginning.</summary>
    public bool isUnlockedByDefault;
}
