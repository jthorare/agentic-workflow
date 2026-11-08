namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What <see cref="AgentRecovery.DecideNode"/> makes of an ended 'run_agent' session's output folder.</summary>
/// <param name="Files">The output file of each declared field present, by field name; empty when parked.</param>
/// <param name="Reason">Why the node is parked, in the words the human reads; null when it completes.</param>
internal sealed record NodeRecoveryDecision(IReadOnlyDictionary<string, string> Files, string? Reason)
{
    /// <summary>True when the node completes from <see cref="Files"/>.</summary>
    public bool Completes => Reason is null;
}
