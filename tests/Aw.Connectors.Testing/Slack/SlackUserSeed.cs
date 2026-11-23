namespace Aw.Connectors.Testing.Slack;

/// <summary>A person the fake Slack knows: what 'users.info' returns.</summary>
/// <param name="Id">Slack's user id ('U…').</param>
/// <param name="Name">The handle (Slack's 'name').</param>
/// <param name="DisplayName">The display name (profile 'display_name').</param>
public sealed record SlackUserSeed(string Id, string Name, string DisplayName)
{
    /// <summary>The full name (Slack's 'real_name').</summary>
    public string RealName { get; init; } = string.Empty;

    /// <summary>The IANA time zone (Slack's 'tz').</summary>
    public string TimeZone { get; init; } = "Etc/UTC";

    /// <summary>The profile title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The profile status text.</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>Whether it is a bot user ('is_bot').</summary>
    public bool IsBot { get; init; }

    /// <summary>Whether the account is deactivated ('deleted').</summary>
    public bool Deleted { get; init; }
}
