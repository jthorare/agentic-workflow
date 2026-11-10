using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The since-you-looked card (<see cref="LinearWorkViewModel"/>, continued; Linear hub spec §3, REQ-9, #2536): a card above the filter bar. It reads from
/// the marker the preferences hold. Mark all seen is the only thing that moves that marker: leaving the view, a reload or a glance never does.
/// The changes are folded into one row per issue and sorted into three fixed groups; the count everywhere (card header, Linear tab, Home line) is that number of issues.
/// </summary>
internal sealed partial class LinearWorkViewModel
{
    private static readonly (LinearSinceGroup Group, string Heading)[] _stripGroups =
    [
        (LinearSinceGroup.Assigned, Labels.LinearSinceAssigned),
        (LinearSinceGroup.Moved, Labels.LinearSinceMoved),
        (LinearSinceGroup.Other, Labels.LinearSinceOther),
    ];

    private int _sinceReads;
    private string? _marker;   // the stored one: what every read asks "after"
    private LinearSince? _since;
    private bool SinceAnswered { get; set; }   // the last read was an answer (not an error, not an older Sindri): "nothing new" may be said
    private Dictionary<string, LinearSinceRowViewModel> _stripShown = new(StringComparer.Ordinal);

    /// <summary>How many issues the card holds (the tab and the Home line carry it).</summary>
    public int NewCount => _stripShown.Count;

    /// <summary>True when the card holds issues (Mark all seen shows).</summary>
    public bool HasNew => NewCount > 0;

    /// <summary>True when the card shows: on the Linear view while connected, with issues, a failed feed or read, or an answered read with nothing new.</summary>
    public bool ShowsStrip => IsLinear && !ShowsConnect && (HasNew || StripFailed || FeedFailing || ShowsNothingNew);

    /// <summary>True when the last read answered and nothing is new: the card says so.</summary>
    public bool ShowsNothingNew => SinceAnswered && !HasNew && !StripFailed && !FeedFailing && !ShowsConnect;

    /// <summary>True when Linear refused the key (SND-CONTEXT-071): the card says why and offers Reconnect, which opens the Connections page.</summary>
    public bool StripNeedsReconnect => FeedFailing && string.Equals(_list?.Feed.Code, "SND-CONTEXT-071", StringComparison.Ordinal);

    /// <summary>True when the feed fails for any other reason (rate limit, a reply Sindri can't read): the card says why and offers Retry.</summary>
    public bool StripFeedFailing => FeedFailing && !StripNeedsReconnect;

    private bool FeedFailing => _list is { } list && string.Equals(list.Feed.State, "failing", StringComparison.Ordinal);

    /// <summary>The card's region name: "Since you last looked, 3 items".</summary>
    public string StripName => Labels.LinearStripName(NewCount);

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>"Nothing new since you last looked".</summary>
    public string NothingNewText => Labels.LinearNothingNew;
#pragma warning restore CA1822, MA0041

    /// <summary>"The last change you saw was 2h ago": the stored marker is the newest change it covered; empty when there is no marker.</summary>
    public string NothingNewWhen => LinearSinceMarker.TryRead(_marker, out var at) ? Labels.LinearNothingNewWhen(_clock.GetUtcNow() - at) : string.Empty;

    /// <summary>The issues with news, in the groups' order (the groups' rows, flattened).</summary>
    public ObservableCollection<LinearSinceRowViewModel> StripRows { get; } = [];

    /// <summary>The card's groups, fixed order (Assigned to you, Moved, Other), empty ones left out.</summary>
    public ObservableCollection<LinearSinceGroupViewModel> StripGroups { get; } = [];

    /// <summary>True when the stored changes could not be read: a smaller state with Retry, apart from the feed's own failure; the list below stays.</summary>
    public bool StripFailed { get; private set; }

    /// <summary>The card's reason, in words: why the feed fails, or that the changes could not be read; empty when neither.</summary>
    public string StripReason => FeedFailing ? Labels.LinearFeedReason(_list?.Feed.Code) : StripFailed ? Labels.LinearStripFailed : string.Empty;

    /// <summary>Why the changes could not be read, from the failed answer's own error; empty when they loaded or the feed's reason is shown.</summary>
    public string StripDetail => StripFailed && !FeedFailing ? StripDetailText : string.Empty;

    private string StripDetailText { get; set; } = string.Empty;

    /// <summary>Mark all seen: stores the answer's marker, so the next read asks after it. The one thing that moves the marker.</summary>
    /// <returns>A task that completes when the marker is written.</returns>
    internal async Task MarkAllSeen()
    {
        if (_since is not { } since)
        {
            return;
        }

        var marker = _marker = since.Marker;
        _sinceReads++;   // a read still on its way asked after the old marker: its answer would show the seen rows again
        Show(since: null);
        await _preferences.Update(p => p with { LinearSince = marker }).ConfigureAwait(true);
    }

    /// <summary>Reconnect: asks for the Connections page (the card's one button when Linear refused the key).</summary>
    internal void Reconnect() => RequestConnect();

    /// <summary>Retry: reads the changes and the list again.</summary>
    /// <returns>A task that completes when the reads are done.</returns>
    internal Task RetryStrip() => _url is { } url ? Reading = ReadAll(url, _shown?.Token ?? CancellationToken.None) : Task.CompletedTask;

    private async Task ReadSince(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_sinceReads;
        var answer = await _api.LinearSince(url, _marker, cancellationToken).ConfigureAwait(true);
        if (read != _sinceReads)
        {
            return;
        }

        if (answer.IsError && OlderSindri.IsNotServed(answer.Error))
        {
            Show(since: null, answered: false);   // an older Sindri has no changes feed: the page says so once, under the filter bar, not here as well
            SindriOlder();
            return;
        }

        if (answer.IsError)
        {
            StripFailed = true;   // the issues already shown stay under the line
            StripDetailText = Labels.InstanceRefused(answer.Error);
            Notify();
            return;
        }

        Show(answer.Value);
    }

    private void Show(LinearSince? since, bool answered = true)
    {
        _since = since is { Count: > 0 } ? since : null;
        SinceAnswered = answered;
        (StripFailed, StripDetailText) = (false, string.Empty);
        var now = _clock.GetUtcNow();
        var kept = new Dictionary<string, LinearSinceRowViewModel>(StringComparer.Ordinal);
        var groups = new List<LinearSinceGroupViewModel>();
        var open = StripGroups.Where(static g => g.IsExpanded).Select(static g => g.Heading).ToHashSet(StringComparer.Ordinal);   // every read makes new groups: keep what Show all opened
        var flat = new List<LinearSinceRowViewModel>();
        var perIssue = (_since?.Items ?? []).GroupBy(static i => i.Key, StringComparer.Ordinal);
        List<LinearSinceRowViewModel> rows = [.. perIssue.Select(g => Row([.. g], now))];
        foreach (var (group, heading) in _stripGroups)
        {
            var inGroup = rows.Where(r => r.Group == group).OrderByDescending(static r => r.At).ToList();
            if (inGroup.Count == 0)
            {
                continue;
            }

            groups.Add(new LinearSinceGroupViewModel(heading, inGroup, open.Contains(heading)));
            flat.AddRange(inGroup);
        }

        foreach (var row in flat)
        {
            kept[row.Key] = row;
        }

        _stripShown = kept;
        Patch.To(StripRows, flat);
        Patch.To(StripGroups, groups);
        EnrichStrip();
        Notify();
    }

    // A row keeps its identity (its focus, its chips) across reads while its changes stay the same.
    private LinearSinceRowViewModel Row(List<LinearSinceItem> changes, DateTimeOffset now)
    {
        var fresh = new LinearSinceRowViewModel(changes, now);
        return _stripShown.TryGetValue(fresh.Key, out var shown) && string.Equals(shown.Detail, fresh.Detail, StringComparison.Ordinal) && shown.At == fresh.At ? shown : fresh;
    }

    // Priority, project and assignee come from the issue list; a row of an issue the list doesn't hold yet shows no such chip.
    private void EnrichStrip()
    {
        foreach (var row in StripRows)
        {
            row.Apply(_rows.TryGetValue(row.Key, out var listed) ? listed.Issue : null);
        }
    }

    private void Notify()
    {
        foreach (var name in new[] { nameof(NewCount), nameof(HasNew), nameof(ShowsStrip), nameof(ShowsNothingNew), nameof(NothingNewWhen), nameof(StripName), nameof(StripFailed), nameof(StripReason), nameof(StripDetail), nameof(StripNeedsReconnect), nameof(StripFeedFailing), nameof(LinearTabText), nameof(LinearTabName) })
        {
            OnChanged(name);
        }
    }
}
