namespace Aw.Sindri.Core.Messages;

/// <summary>How a new message reaches me (messages spec §7.2 "Done and return"): the first three bring a done thread back, a reaction never does.</summary>
public enum MessageAddress
{
    /// <summary>Addressed to someone else, or a broadcast.</summary>
    Other,

    /// <summary>It names me.</summary>
    Mention,

    /// <summary>A DM or group DM.</summary>
    DirectMessage,

    /// <summary>A reply in a thread I wrote in.</summary>
    ReplyInMyThread,

    /// <summary>A reaction to my message.</summary>
    ReactionToMine,
}
