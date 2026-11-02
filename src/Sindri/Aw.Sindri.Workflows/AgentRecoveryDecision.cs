using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What <see cref="AgentRecovery.Decide"/> makes of an ended agent run's output folder: complete the run from its outputs, or park it with a reason.</summary>
/// <param name="Outcome">The outcome to report; null when the run is parked instead.</param>
/// <param name="Files">The output file that holds each fact, by fact key, when the run completes; empty when parked.</param>
/// <param name="Reason">Why the run is parked, in the words the human reads; null when it completes.</param>
internal sealed record AgentRecoveryDecision(OutcomeName? Outcome, IReadOnlyDictionary<FactKey, string> Files, string? Reason)
{
    /// <summary>The run completes with <paramref name="outcome"/> from <paramref name="files"/>.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="files">The file of each fact.</param>
    /// <returns>The decision.</returns>
    public static AgentRecoveryDecision Complete(OutcomeName outcome, IReadOnlyDictionary<FactKey, string> files) => new(outcome, files, Reason: null);

    /// <summary>The run is parked for <paramref name="reason"/>.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The decision.</returns>
    public static AgentRecoveryDecision Park(string reason) => new(Outcome: null, new Dictionary<FactKey, string>(), reason);
}
