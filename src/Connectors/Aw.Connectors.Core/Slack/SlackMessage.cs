namespace Aw.Connectors.Core.Slack;

/// <summary>One Slack message as the Web API returns it.</summary>
/// <param name="Ts">Slack's timestamp, which is also the message's id in its channel ('1700000000.000100').</param>
/// <param name="ThreadTs">The root's ts when the message is in a thread (the root has its own ts), or null.</param>
/// <param name="Author">The author's display name, or null when the payload gives none (a user id is never used).</param>
/// <param name="Text">The text as written (mrkdwn).</param>
/// <param name="ReplyCount">For a thread root, the number of replies; 0 otherwise.</param>
/// <param name="Subtype">Slack's subtype ('channel_join', 'bot_message'), or null for an ordinary message.</param>
/// <param name="LatestReply">For a thread root, the ts of its newest reply (Slack's 'latest_reply'); null when Slack gives none.</param>
public sealed record SlackMessage(string Ts, string? ThreadTs, string? Author, string Text, int ReplyCount, string? Subtype, string? LatestReply = null);
