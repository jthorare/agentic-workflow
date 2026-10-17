using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// A Step run reported an outcome (spec §3.5, §5.3): from the agent runtime, or a desk report ('workflow complete
/// --run'). Valid only for the open run's token (SND-WF-005); a malformed report is rejected first (SND-WF-008).
/// </summary>
/// <param name="Actor">Who reported it.</param>
/// <param name="Run">The run's token.</param>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">The facts it wrote, as stored references.</param>
/// <param name="Tokens">The tokens the run spent (0 for a desk report).</param>
/// <param name="Via">The runtime that ran it.</param>
/// <param name="ReportSha256">The SHA-256 of the canonical report, so two different reports never share a key.</param>
/// <param name="Decision">The Judge decision id a block Step reports ('JD-…'), for the event's 'decision'; null for any other.</param>
public sealed record StepCompleted(
    string Actor,
    RunToken Run,
    OutcomeName Outcome,
    IReadOnlyDictionary<FactKey, FactValue> Outputs,
    int Tokens,
    StepRuntime Via,
    string ReportSha256,
    string? Decision = null) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.StepCompleted;

    /// <inheritdoc/>
    public override string KeyPart => "run:" + Run.Value + ":" + ReportSha256;
}
