namespace Aw.Context.Core.Engine;

/// <summary>One listed Slack channel: its Slack id and the name people know it by.</summary>
/// <param name="Channel">The channel id Slack gives it (the key records carry: 'C' or 'G' then capitals and digits).</param>
/// <param name="Name">The channel name.</param>
public sealed record SlackChannel(string Channel, string Name);
