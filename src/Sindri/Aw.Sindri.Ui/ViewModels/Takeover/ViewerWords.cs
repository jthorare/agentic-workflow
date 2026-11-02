using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Takeover;

/// <summary>What a launch's viewer came to, in words: what it did, whether the attach command is shown, and a takeover's outcome.</summary>
internal static class ViewerWords
{
    private const string Print = "print";

    /// <summary>What a started launch's viewer did, in words: opened, unconfirmed, failed (code and detail) or skipped (the served reason).</summary>
    /// <param name="viewer">The viewer as served.</param>
    /// <returns>The line.</returns>
    internal static string ViewerLine(LaunchViewerView viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return viewer.Outcome switch
        {
            "opened" => Labels.LaunchOpenedIn(viewer.Label ?? viewer.Kind),
            "unconfirmed" => Labels.LaunchUnconfirmed(viewer.Label ?? viewer.Kind),
            "failed" when viewer.Reason is { Length: > 0 } reason => Labels.LaunchViewerSkipped(reason),   // a served reason is the sentence; code and detail otherwise
            "failed" => Labels.LaunchViewerFailed(viewer.Code ?? "?", viewer.Detail ?? string.Empty),
            _ => viewer.Reason is { Length: > 0 } reason ? Labels.LaunchViewerSkipped(reason) : Labels.LaunchRunning,
        };
    }

    /// <summary>True when there is an attach command and no viewer opened or may have opened the terminal: no viewer, the print viewer, or one that failed or was skipped.</summary>
    /// <param name="viewer">The viewer as served, or null.</param>
    /// <param name="attach">The attach command as served, or null.</param>
    /// <returns>True when the command is shown.</returns>
    internal static bool ShowsCommand(LaunchViewerView? viewer, IReadOnlyList<string>? attach) =>
        attach is { Count: > 0 }
        && (viewer is null
            || string.Equals(viewer.Kind, Print, StringComparison.Ordinal)
            || string.Equals(viewer.Outcome, "failed", StringComparison.Ordinal)
            || string.Equals(viewer.Outcome, "skipped", StringComparison.Ordinal));

    /// <summary>What a takeover's viewer came to: the line, and the attach command when the person must run it.</summary>
    /// <param name="viewer">The viewer as served, or null when none was served.</param>
    /// <param name="attach">The attach command as served, or null.</param>
    /// <returns>The line and the command (empty when none is shown).</returns>
    internal static (string Line, string Command) TakeoverOutcome(LaunchViewerView? viewer, IReadOnlyList<string>? attach)
    {
        var command = ShowsCommand(viewer, attach) ? string.Join(' ', attach!) : string.Empty;   // why: ShowsCommand is true only when attach is non-empty
        var line = viewer is null ? (command.Length > 0 ? Labels.AttachRunThis : Labels.TakeoverNoViewer)
            : command.Length > 0 && string.Equals(viewer.Kind, Print, StringComparison.Ordinal) ? Labels.AttachRunThis
            : ViewerLine(viewer);
        return (line, command);
    }
}
