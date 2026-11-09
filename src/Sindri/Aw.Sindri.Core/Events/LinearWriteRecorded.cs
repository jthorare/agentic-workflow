namespace Aw.Sindri.Core.Events;

/// <summary>The payload of 'linear.issue.created', 'linear.comment.posted' and 'linear.issue.changed': a write Sindri sent to Linear for a caller (a create's or comment's client id is in the event key) (the envelope's actor says who: the owner or 'session:launch:&lt;id&gt;').</summary>
/// <param name="Team">The team's key, 'ENG'.</param>
/// <param name="Issue">The issue's key, 'ENG-12'; null when a create was applied but Sindri has no key for it yet.</param>
/// <param name="Fields">The names of the fields a change set, comma-separated; null for a create or a comment.</param>
public sealed record LinearWriteRecorded(string Team, string? Issue, string? Fields);
