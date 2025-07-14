/// <summary>
/// Attachment applied to the avatar for purely visual customization.
/// </summary>
[Serializable]
public class VisualAttachment
{
    /// <summary>Unique id of the attachment asset.</summary>
    public string attachmentId = string.Empty;
    /// <summary>Name of the mount point on the rig.</summary>
    public string socket = string.Empty;
}
