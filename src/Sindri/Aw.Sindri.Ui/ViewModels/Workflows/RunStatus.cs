using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The one status a run shows on a board card, a list row and the run drawer (F3 workflows spec §4): the served chip's word
/// mapped to a kind, a sentence-case word, a glyph and a colour class. A word this build does not know shows as served, neutral.
/// </summary>
/// <param name="Kind">The kind of status.</param>
/// <param name="Word">The word the user reads.</param>
/// <param name="Icon">The Icons.axaml name of the glyph drawn beside the word.</param>
/// <param name="Class">The style class that colours the badge.</param>
internal sealed record RunStatus(RunStatusKind Kind, string Word, string Icon, string Class)
{
    /// <summary>Maps a served chip to the run's status.</summary>
    /// <param name="chip">The chip the API served.</param>
    /// <returns>The status to draw.</returns>
    internal static RunStatus From(StateChip chip) => Normal(chip.Word) switch
    {
        "RUNNING" => new(RunStatusKind.Running, Labels.WorkflowsStatusRunning, "Spinner", "running"),
        "DECISION" or "ESCALATED" => new(RunStatusKind.WaitingForYou, Labels.WorkflowsStatusWaitingForYou, "RunClock", "attn"),
        "PARKED" => new(RunStatusKind.Paused, Labels.WorkflowsStatusPaused, "RunPause", "attn"),
        "WITH YOU" => new(RunStatusKind.TookOver, Labels.WorkflowsStatusTookOver, "RunPerson", "violet"),
        "QUEUED" => new(RunStatusKind.NotStarted, Labels.WorkflowsStatusNotStarted, "RunCircleDashed", "faint"),
        "DONE" => new(RunStatusKind.Done, Labels.WorkflowsStatusDone, "Ok", "ok"),
        "FAILED" => new(RunStatusKind.Failed, Labels.WorkflowsStatusFailed, "RunCircleX", "fail"),
        "DIDN'T WORK OUT" => new(RunStatusKind.DidntWorkOut, Labels.WorkflowsStatusDidntWorkOut, "RunCircleMinus", "attn"),
        "CANCELLED" => new(RunStatusKind.Cancelled, Labels.WorkflowsStatusCancelled, "RunCircleX", "neutral"),
        _ => new(RunStatusKind.Unknown, chip.Word, "RunCircleDashed", "neutral"),
    };

    // The served word in capitals with a straight apostrophe, so a curly one or another case still maps.
    private static string Normal(string word) => word.Trim().ToUpperInvariant().Replace('\u2019', '\'');
}
