namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A Step run failed (spec §3.5): a recipe transition on 'step.failed' takes it, otherwise the instance parks with 'needs-answer'.</summary>
/// <param name="Actor">Who reported it.</param>
/// <param name="Run">The run's token.</param>
/// <param name="Reason">What failed, in the words the human reads (scrubbed before it is recorded).</param>
/// <param name="Tokens">The tokens the run spent.</param>
/// <param name="ReportSha256">The SHA-256 of the canonical report.</param>
public sealed record StepFailed(string Actor, RunToken Run, string Reason, int Tokens, string ReportSha256) : Trigger(Actor)
{
    /// <summary>The reason of a run whose launch failed before any session started (the agent runtime records it; the loop counts no round for it).</summary>
    public const string LaunchReason = "launch";

    /// <summary>What the human reads after the reason (an agent run's launcher words, scrubbed); null for none. It is in the park's question, not in the recorded reason.</summary>
    public string? Detail { get; init; }

    /// <summary>The typed code of the dependency's own failure (SND-LAUNCH-007, SND-WF-016 …) where there is one; null otherwise. It becomes the park's code.</summary>
    public string? Code { get; init; }

    /// <summary>Why an agent run failed: <see cref="AgentFailureKinds"/> (refused, check_failed, crashed); null for any other failure and for rows recorded before this field.</summary>
    public string? FailureKind { get; init; }

    /// <summary>The dependency that was down, in the words the park names it (an input's block id, the launcher …); null names the Step itself.</summary>
    public string? Dependency { get; init; }

    /// <inheritdoc/>
    public override string Name => TriggerNames.StepFailed;

    /// <inheritdoc/>
    public override string KeyPart => "run:" + Run.Value + ":" + ReportSha256;
}
