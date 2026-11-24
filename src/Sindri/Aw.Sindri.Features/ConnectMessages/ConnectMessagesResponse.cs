namespace Aw.Sindri.Features.ConnectMessages;

/// <summary>What connecting stored (messages spec §5.1): the connection 'slack:messages' with the workspace and the user auth.test returned, or the manifest.</summary>
/// <param name="Connection">The connection's name ('slack:messages'), or null for a manifest answer.</param>
/// <param name="Workspace">The Slack workspace auth.test returned, or null.</param>
/// <param name="User">My Slack user auth.test returned, or null.</param>
/// <param name="Manifest">The app manifest YAML, or null when a token was stored.</param>
public sealed record ConnectMessagesResponse(string? Connection, string? Workspace, string? User, string? Manifest);
