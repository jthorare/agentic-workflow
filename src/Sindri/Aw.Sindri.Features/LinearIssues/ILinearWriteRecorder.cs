using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Records the Linear writes Sindri sent, one ledger event per write, under the current caller's actor (the owner, or 'session:launch:&lt;id&gt;' for a launched session).</summary>
internal interface ILinearWriteRecorder
{
    /// <summary>Appends the event of a write Linear applied.</summary>
    /// <param name="write">The write.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>SND-API-001 when the control plane doesn't hold the lease, or the ledger's error; null when it is recorded.</returns>
    public ValueTask<Error?> Record(LinearWrite write, CancellationToken cancellationToken);
}

/// <summary>One Linear write Sindri sent, as its event records it.</summary>
/// <param name="Type">'linear.issue.created', 'linear.comment.posted' or 'linear.issue.changed'.</param>
/// <param name="Team">The team's key.</param>
/// <param name="Issue">The issue's key, or null when a create has none yet.</param>
/// <param name="ClientId">The client id of a create or a comment; null for a change.</param>
/// <param name="Fields">The field names a change set, comma-separated; null otherwise.</param>
internal sealed record LinearWrite(string Type, string Team, string? Issue, string? ClientId, string? Fields);
