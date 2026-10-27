namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>The values of <see cref="StepFailed.FailureKind"/> for an agent Step (model-pick spec 7.1): why the run failed, so an escalation and the model history can tell them apart.</summary>
public static class AgentFailureKinds
{
    /// <summary>The report was refused (ReportRefusals took its text).</summary>
    public const string Refused = "refused";

    /// <summary>The report was read and the Step failed its check.</summary>
    public const string CheckFailed = "check_failed";

    /// <summary>The session ended with no report: an exit with no refusal, lost, idle, or its launch failed.</summary>
    public const string Crashed = "crashed";

    /// <summary>The kind of a failed agent run.</summary>
    /// <param name="refused">True when the session's report was refused (the ended session's AgentSessionState.Refused).</param>
    /// <param name="checkFailed">True when the output folder was read and the Step failed its check.</param>
    /// <returns>refused, check_failed or crashed.</returns>
    public static string Of(bool refused, bool checkFailed) =>
        refused ? Refused : checkFailed ? CheckFailed : Crashed;
}
