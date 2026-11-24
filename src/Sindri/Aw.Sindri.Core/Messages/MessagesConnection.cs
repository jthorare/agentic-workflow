namespace Aw.Sindri.Core.Messages;

/// <summary>The 'slack:messages' connection (messages spec §5.1): hand-made, Preset null, the workspace and user ids in its Config and a probeUrl for auth.test.</summary>
public static class MessagesConnection
{
    /// <summary>The connection's name; only 'sindri messages connect' writes it.</summary>
    public const string Name = "slack:messages";

    /// <summary>The Config key of the Slack workspace (team) id auth.test returned.</summary>
    public const string WorkspaceKey = "workspace";

    /// <summary>The Config key of my Slack user id auth.test returned.</summary>
    public const string UserKey = "user";

    /// <summary>The Config key the connection test reads (TestConnectionService): without it a Preset-null connection reports "connected" with no call.</summary>
    public const string ProbeUrlKey = "probeUrl";

    /// <summary>The URL the connection test calls: Slack's auth.test.</summary>
    public static Uri ProbeUrl { get; } = new(SlackBaseUrlRule.Slack, "auth.test");

    /// <summary>The connection's non-secret Config: the workspace and user auth.test returned, and the probeUrl the connection test calls.</summary>
    /// <param name="workspace">The Slack workspace.</param>
    /// <param name="user">My Slack user.</param>
    /// <returns>The Config map.</returns>
    public static IReadOnlyDictionary<string, string> Config(string workspace, string user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkspaceKey] = workspace,
            [UserKey] = user,
            [ProbeUrlKey] = ProbeUrl.AbsoluteUri,
        };
    }
}
