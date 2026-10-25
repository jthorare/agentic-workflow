using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Events;

/// <summary>The viewer part of 'launch.started' (launcher L-10). <see cref="Detail"/> is the viewer's redacted stderr and stays out of <see cref="LaunchViewerPublic"/>.</summary>
/// <param name="Kind">The viewer kind ('warp', 'exec-template', 'print'; for a background launch the configured viewer's kind, which opens on takeover, or 'none' when no viewer could be probed).</param>
/// <param name="Label">What a person calls it, or null.</param>
/// <param name="Outcome">What it did.</param>
/// <param name="Code">The error code for a failure, otherwise null.</param>
/// <param name="Detail">The viewer's redacted stderr for a failure, otherwise null.</param>
public sealed record LaunchViewer(string Kind, string? Label, ViewerOutcomeKind Outcome, string? Code, string? Detail)
{
    /// <summary>The reason a viewer was skipped on purpose, otherwise null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>The viewer of a background (unattended) launch: skipped on purpose, naming the configured viewer that opens on takeover (kind 'none' when the launcher couldn't say).</summary>
    /// <param name="outcome">What the launcher reported: its viewer kind and label when it probed one, and the reason.</param>
    /// <returns>The recorded form.</returns>
    public static LaunchViewer Background(ViewerOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new LaunchViewer(outcome.ViewerKind ?? "none", outcome.ViewerLabel, outcome.Kind, outcome.Code, outcome.Detail) { Reason = outcome.Reason };
    }
}
