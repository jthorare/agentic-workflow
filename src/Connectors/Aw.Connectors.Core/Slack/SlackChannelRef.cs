namespace Aw.Connectors.Core.Slack;

/// <summary>One listed Slack channel: the id Slack gives it (what the allowlist and the record keys use) and the name people know it by (display only).</summary>
/// <param name="Channel">The channel id: 'C' or 'G', then capitals and digits. Never a direct message.</param>
/// <param name="Name">The channel name, without the '#'.</param>
public sealed record SlackChannelRef(string Channel, string Name);
