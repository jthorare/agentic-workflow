namespace Aw.Sindri.Core.Messages;

/// <summary>What Slack's auth.test said about a token (messages spec §5.1): the workspace (team) and user it belongs to.</summary>
/// <param name="Workspace">The Slack workspace (team) id, 'T…'.</param>
/// <param name="User">My Slack user id, 'U…' or 'W…'.</param>
/// <param name="Enterprise">The Enterprise Grid org id, or null outside a grid.</param>
public sealed record SlackAuth(string Workspace, string User, string? Enterprise);
