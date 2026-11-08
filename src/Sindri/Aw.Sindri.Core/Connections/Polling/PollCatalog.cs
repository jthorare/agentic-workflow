namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>The v1 pollable events per preset (Spotify has none).</summary>
public static class PollCatalog
{
    private static readonly EventKind[] _github =
    [
        EventKind.GitHubPrOpened, EventKind.GitHubPrMerged, EventKind.GitHubPrReviewRequested,
        EventKind.GitHubIssueOpened, EventKind.GitHubIssueLabeled, EventKind.GitHubPush,
    ];

    private static readonly EventKind[] _linear =
    [
        EventKind.LinearIssueCreated, EventKind.LinearIssueStateChanged, EventKind.LinearIssueLabeled, EventKind.LinearCommentAdded,
    ];

    private static readonly EventKind[] _slack = [EventKind.SlackMessage, EventKind.SlackMention];

    private static readonly EventKind[] _google = [EventKind.GoogleCalendarEventStarting];

    /// <summary>Lists the events a preset can be polled for.</summary>
    /// <param name="preset">The preset key ('github').</param>
    /// <returns>The events in catalog order; empty for a preset with none.</returns>
    public static IReadOnlyList<EventKind> For(string preset) => preset switch
    {
        "github" => _github,
        "linear" => _linear,
        "slack" => _slack,
        "google" => _google,
        _ => [],
    };

    /// <summary>Finds an event by catalog id.</summary>
    /// <param name="key">The catalog id ('github.pr.merged').</param>
    /// <returns>The kind, or null when the catalog has no such event.</returns>
    public static EventKind? Find(string key)
    {
        foreach (var kind in _github.Concat(_linear).Concat(_slack).Concat(_google))
        {
            if (string.Equals(kind.Key, key, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        return null;
    }
}
