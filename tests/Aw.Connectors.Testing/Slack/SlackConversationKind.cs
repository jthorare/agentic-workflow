namespace Aw.Connectors.Testing.Slack;

/// <summary>The four conversation kinds Slack's 'users.conversations' lists by its 'types' argument.</summary>
public enum SlackConversationKind
{
    /// <summary>A public channel ('public_channel').</summary>
    Public,

    /// <summary>A private channel ('private_channel').</summary>
    Private,

    /// <summary>A direct message with one person ('im').</summary>
    Im,

    /// <summary>A group direct message ('mpim').</summary>
    Mpim,
}
