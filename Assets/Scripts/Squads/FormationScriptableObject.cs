using UnityEngine;

/// <summary>
/// Defines a formation pattern relative to the squad leader.
/// </summary>
[CreateAssetMenu(menuName = "Formations/Formation Pattern")]
public class FormationScriptableObject : ScriptableObject
{
    /// <summary>Formation this pattern represents.</summary>
    public FormationType formationType;

    /// <summary>Offsets relative to the leader for each unit (x, z).</summary>
    public Vector2[] localOffsets;
}
