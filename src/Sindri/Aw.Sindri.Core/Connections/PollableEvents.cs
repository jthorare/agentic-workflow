namespace Aw.Sindri.Core.Connections;

/// <summary>
/// The v1 pollable events per preset, by catalog id (Spotify has none): the one list. <see cref="ConnectionPreset.PollableEvents"/> and
/// the poll catalog (Polling.PollCatalog) both read it, so the presets never depend on the polling namespace.
/// </summary>
public static class PollableEvents
{
    private static readonly string[] _github =
        [
            "github.pr.opened", "github.pr.updated", "github.pr.ready_for_review", "github.pr.closed", "github.pr.merged", "github.pr.review_requested", "github.pr.review_submitted",
            "github.pr.comment", "github.pr.checks", "github.pr.conflict", "github.issue.opened", "github.issue.labeled", "github.push",
        ];

    private static readonly string[] _linear =
        ["linear.issue.created", "linear.issue.state_changed", "linear.issue.labeled", "linear.comment.added"];

    private static readonly string[] _slack = ["slack.message", "slack.mention"];

    private static readonly string[] _google = ["google.calendar.event_starting"];

    /// <summary>Gets the presets that have pollable events, in catalog order.</summary>
    public static IReadOnlyList<string> Presets { get; } = ["github", "linear", "slack", "google"];

    /// <summary>Lists the catalog ids a preset can be polled for.</summary>
    /// <param name="preset">The preset key ('github').</param>
    /// <returns>The ids in catalog order; empty for a preset with none.</returns>
    public static IReadOnlyList<string> For(string preset) => preset switch
    {
        "github" => _github,
        "linear" => _linear,
        "slack" => _slack,
        "google" => _google,
        _ => [],
    };
}
