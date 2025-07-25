using System;
using System.Collections.Generic;

/// <summary>
/// Visual customization references for the hero's modular avatar.
/// </summary>
[Serializable]
public class AvatarParts
{
    /// <summary>Identifier of the head mesh or prefab.</summary>
    public string headId = string.Empty;
    /// <summary>Identifier for the hair style.</summary>
    public string hairId = string.Empty;
    /// <summary>Identifier for facial hair or beard.</summary>
    public string beardId = string.Empty;
    /// <summary>Identifier for the eyebrow style.</summary>
    public string eyebrowId = string.Empty;
}