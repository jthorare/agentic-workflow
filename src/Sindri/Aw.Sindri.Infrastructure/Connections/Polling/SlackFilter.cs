using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>What a Slack poll looks for: the event kind, the channel, the text filter ('' for any) and the owner's user id ('' for none).</summary>
/// <param name="Kind">The event kind.</param>
/// <param name="Channel">The channel id.</param>
/// <param name="Text">The word the text has to contain, or an empty string.</param>
/// <param name="OwnerId">The owner's Slack user id, or an empty string.</param>
internal sealed record SlackFilter(EventKind Kind, string Channel, string Text, string OwnerId);
