namespace Aw.Sindri.Features.LinearIssues;

/// <summary>One Linear write Sindri sent, as its event records it.</summary>
/// <param name="Type">'linear.issue.created', 'linear.comment.posted' or 'linear.issue.changed'.</param>
/// <param name="Team">The team's key.</param>
/// <param name="Issue">The issue's key, or null when a create has none yet.</param>
/// <param name="ClientId">The client id of a create or a comment; null for a change.</param>
/// <param name="Fields">The field names a change set, comma-separated; null otherwise.</param>
internal sealed record LinearWrite(string Type, string Team, string? Issue, string? ClientId, string? Fields);
