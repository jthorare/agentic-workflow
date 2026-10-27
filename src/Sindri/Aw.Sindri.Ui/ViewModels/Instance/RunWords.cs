using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// An agent run in words: the launch's state, why Attach waits, and what happened to the agent's Steps. Every sentence is a
/// served field mapped to the app's own copy; a code or a detail is shown as served.
/// </summary>
internal static class RunWords
{
    /// <summary>The launch's state: requested, started (with what its viewer did) or failed (with its code and detail).</summary>
    /// <param name="launch">The launch as served.</param>
    /// <returns>The line.</returns>
    internal static string LaunchLine(LaunchView launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (string.Equals(launch.State, "failed", StringComparison.Ordinal))
        {
            return Labels.LaunchFailed(launch.Code ?? "?", launch.Detail ?? string.Empty);
        }

        if (!string.Equals(launch.State, "started", StringComparison.Ordinal))
        {
            return Labels.LaunchRequested;
        }

        return launch.Viewer is not { } viewer ? Labels.LaunchRunning : ViewerWords.ViewerLine(viewer);
    }

    /// <summary>True when the launch carries an attach command to show (no viewer opened the terminal).</summary>
    /// <param name="launch">The launch as served.</param>
    /// <returns>True when the command is shown.</returns>
    internal static bool ShowsCommand(LaunchView launch) => ViewerWords.ShowsCommand(launch.Viewer, launch.Attach);

    /// <summary>The attach command as one line.</summary>
    /// <param name="launch">The launch as served.</param>
    /// <returns>The line, or empty.</returns>
    internal static string Command(LaunchView launch) => launch.Attach is { } argv ? string.Join(' ', argv) : string.Empty;

    /// <summary>True when the item serves an Attach: the mode is offered and a live session is named.</summary>
    /// <param name="launch">The item's launch block, or null.</param>
    /// <returns>True when ready.</returns>
    internal static bool AttachReady(ItemLaunchView? launch) =>
        launch is { Attach: not null } && launch.Modes.Contains("attach", StringComparer.Ordinal);

    /// <summary>Why Attach is not ready, from what the item serves; null when it is ready.</summary>
    /// <param name="item">The item as read, or null when not read.</param>
    /// <param name="unreadable">True when the read failed (as opposed to not made yet).</param>
    /// <returns>The reason, or null.</returns>
    internal static string? PendingReason(ItemView? item, bool unreadable)
    {
        if (item is null)
        {
            return unreadable ? Labels.AttachItemUnreadable : Labels.AttachChecking;
        }

        var launch = item.Launch;
        if (AttachReady(launch))
        {
            return null;
        }

        return launch is null ? Labels.AttachItemUnreadable
            : launch.InFlight is not null ? Labels.AttachLaunchStarting
            : launch.Blockers.Count > 0 ? launch.Blockers[0].Detail
            : Labels.AttachNoLiveSession;
    }

    /// <summary>What an agent's Steps came to, newest last: "via agent · N tokens" for a completion, a plain sentence for a failure.</summary>
    /// <param name="events">The instance's events.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> EventLines(IReadOnlyList<InstanceEventRow> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var lines = new List<string>();
        foreach (var fields in events.Select(e => e.Transition).OfType<InstanceEventFields>())
        {
            if (string.Equals(fields.Trigger, "step.failed", StringComparison.Ordinal) && (fields.Via is null || string.Equals(fields.Via, "agent", StringComparison.Ordinal)) && fields.Reason is not null)
            {
                lines.Add(Labels.AgentFailed(fields.Reason, fields.To));
            }
            else if (string.Equals(fields.Via, "agent", StringComparison.Ordinal) && string.Equals(fields.Trigger, "step.completed", StringComparison.Ordinal))
            {
                lines.Add(Labels.ViaAgent(fields.Tokens));
            }
        }

        return lines;
    }
}
