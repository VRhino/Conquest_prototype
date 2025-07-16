using System;

/// <summary>
/// Attachment applied to the avatar for purely visual customization.
/// </summary>
[Serializable]
public class VisualAttachment
{
    /// <summary>Unique id of the visual asset.</summary>
    public string visualID = string.Empty;
    /// <summary>Name of the mount point on the male rig.</summary>
    public string boneTargetMale = string.Empty;
    /// <summary>Name of the mount point on the female rig.</summary>
    public string boneTargetFemale = string.Empty;
    /// <summary>Prefab path for male.</summary>
    public string prefabPathMale = string.Empty;
    /// <summary>Prefab path for female.</summary>
    public string prefabPathFemale = string.Empty;
}
