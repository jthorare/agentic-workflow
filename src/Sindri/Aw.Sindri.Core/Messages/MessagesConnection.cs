namespace Aw.Sindri.Core.Messages;

/// <summary>The 'slack:messages' connection (messages spec §5.1): hand-made, Preset null, the workspace and user ids in its Config and a probeUrl for auth.test.</summary>
public static class MessagesConnection
{
    /// <summary>The connection's name; only 'sindri messages connect' writes it.</summary>
    public const string Name = "slack:messages";

    /// <summary>The connection's non-secret Config: the workspace and user auth.test returned, and the probeUrl the connection test calls.</summary>
    /// <param name="workspace">The Slack workspace.</param>
    /// <param name="user">My Slack user.</param>
    /// <returns>The Config map.</returns>
    public static IReadOnlyDictionary<string, string> Config(string workspace, string user)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(user);
        throw MessageStubs.NotBuilt("MessagesConnection.Config", "M1d");
    }
}
