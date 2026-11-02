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
/// <param name="FindingKeys">The sorted keys of the findings the report carries (<see cref="Rules.FindingKeys"/>), or null when it has none in the keyed shape (the digest rule applies).</param>
/// <param name="EmptyDiff">Whether the engine measured an empty diff for an agent run (never self-reported).</param>
/// <param name="Decision">The Judge decision id a block Step reports ('JD-…'), for the event's 'decision'; null for any other.</param>
public sealed record StepCompleted(
    string Actor,
    RunToken Run,
    OutcomeName Outcome,
    IReadOnlyDictionary<FactKey, FactValue> Outputs,
    int Tokens,
    StepRuntime Via,
    string ReportSha256,
    string? Decision = null,
    IReadOnlyList<string>? FindingKeys = null,
    bool EmptyDiff = false) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.StepCompleted;

    /// <summary>The launcher agent an agent run ran on, for the event's 'agent' (#1269); null for any other Step.</summary>
    public string? Agent { get; init; }

    /// <summary>The model an agent run asked for, for the event's 'model'; null when none was named and for any other Step.</summary>
    public string? Model { get; init; }

    /// <inheritdoc/>
    public override string KeyPart => "run:" + Run.Value + ":" + ReportSha256;
}
