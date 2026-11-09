namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What <see cref="AgentRecovery.DecideNode"/> makes of an ended 'run_agent' node run's output folder: complete the node from these files, or park it with a reason.</summary>
/// <param name="Names">The files that hold the node's declared outputs, one per output present; empty when parked.</param>
/// <param name="Reason">Why the node can't complete from the folder, in the words a person reads; null when it completes.</param>
internal sealed record AgentNodeRecovery(IReadOnlyList<string> Names, string? Reason)
{
    /// <summary>The node completes from <paramref name="names"/>.</summary>
    /// <param name="names">The files.</param>
    /// <returns>The decision.</returns>
    public static AgentNodeRecovery Complete(IReadOnlyList<string> names) => new(names, Reason: null);

    /// <summary>The node can't complete from the folder.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The decision.</returns>
    public static AgentNodeRecovery Park(string reason) => new([], reason);
}
