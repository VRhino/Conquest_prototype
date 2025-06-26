using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Component representing a single chat message.
/// Created on the client when a player sends a message
/// and removed after it has been processed by <see cref="ChatSystem"/>.
/// </summary>
public struct ChatMessageComponent : IComponentData
{
    /// <summary>Name of the player sending the message.</summary>
    public FixedString64Bytes senderName;

    /// <summary>Text content of the chat message.</summary>
    public FixedString128Bytes message;

    /// <summary>
    /// True if the message should only be delivered to team mates.
    /// The MVP always sets this to true.
    /// </summary>
    public bool teamOnly;
}
