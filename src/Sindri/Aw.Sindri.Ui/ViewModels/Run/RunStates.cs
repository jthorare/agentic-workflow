using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The one map from a served chip word to the run canvas's state, and from a state to its word and brush keys (F3 spec 3 §3).</summary>
internal static class RunStates
{
    private static readonly RunStateBrushes _running = new("Brush.PrimaryHover", "Brush.PrimarySoft", "Brush.PrimaryHover");
    private static readonly RunStateBrushes _done = new("Brush.Success", "Brush.SuccessSoft", "Brush.Success");
    private static readonly RunStateBrushes _failed = new("Brush.Error", "Brush.ErrorSoft", "Brush.Error");
    private static readonly RunStateBrushes _attention = new("Brush.Attention", "Brush.AttentionSoft", "Brush.Attention");
    private static readonly RunStateBrushes _cancelled = new("Brush.InkMuted", "Brush.Surface2", "Brush.InkSubtle");
    private static readonly RunStateBrushes _tookOver = new("Brush.Violet", "Brush.VioletSoft", "Brush.Violet");
    private static readonly RunStateBrushes _notStarted = new("Brush.InkSubtle", "Brush.Surface2", "Brush.HairlineTertiary");

    /// <summary>The state a chip names.</summary>
    /// <param name="chip">The chip as served.</param>
    /// <returns>The state; a word this build does not know reads as not started.</returns>
    internal static RunNodeState FromChip(StateChip chip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        return chip.Word.ToUpperInvariant() switch
        {
            "RUNNING" => RunNodeState.Running,
            "DONE" => RunNodeState.Done,
            "FAILED" => RunNodeState.Failed,
            "DIDN'T WORK OUT" => RunNodeState.DidntWorkOut,
            "CANCELLED" => RunNodeState.Cancelled,
            "DECISION" => RunNodeState.Waiting,
            "PARKED" or "ESCALATED" => RunNodeState.Paused,
            "WITH YOU" => RunNodeState.TookOver,
            _ => RunNodeState.NotStarted,
        };
    }

    /// <summary>The word for a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The word, in sentence case.</returns>
    internal static string Word(RunNodeState state) => state switch
    {
        RunNodeState.Running => Labels.RunWordRunning,
        RunNodeState.Done => Labels.RunWordDone,
        RunNodeState.Failed => Labels.RunWordFailed,
        RunNodeState.DidntWorkOut => Labels.RunWordDidntWorkOut,
        RunNodeState.Cancelled => Labels.RunWordCancelled,
        RunNodeState.Waiting => Labels.RunWordWaiting,
        RunNodeState.Paused => Labels.RunWordPaused,
        RunNodeState.TookOver => Labels.RunWordTookOver,
        RunNodeState.NotStarted => Labels.RunWordNotStarted,
        _ => Labels.RunWordNotStarted,
    };

    /// <summary>The brush keys a state is drawn with.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The keys.</returns>
    internal static RunStateBrushes Brushes(RunNodeState state) => state switch
    {
        RunNodeState.Running => _running,
        RunNodeState.Done => _done,
        RunNodeState.Failed => _failed,
        RunNodeState.DidntWorkOut or RunNodeState.Waiting or RunNodeState.Paused => _attention,
        RunNodeState.Cancelled => _cancelled,
        RunNodeState.TookOver => _tookOver,
        RunNodeState.NotStarted => _notStarted,
        _ => _notStarted,
    };

    /// <summary>Whether a state is one a person is in the middle of (running, waiting, paused or taken over).</summary>
    /// <param name="state">The state.</param>
    /// <returns>True when the state is the run's active one.</returns>
    internal static bool IsActive(RunNodeState state) => state is RunNodeState.Running or RunNodeState.Waiting or RunNodeState.Paused or RunNodeState.TookOver;

    /// <summary>Whether a state is an ending that was not a success (failed, didn't work out, cancelled).</summary>
    /// <param name="state">The state.</param>
    /// <returns>True when the state ended badly.</returns>
    internal static bool EndedBadly(RunNodeState state) => state is RunNodeState.Failed or RunNodeState.DidntWorkOut or RunNodeState.Cancelled;
}
