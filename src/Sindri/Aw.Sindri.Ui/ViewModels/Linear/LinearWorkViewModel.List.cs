using System.Collections.ObjectModel;
using System.Globalization;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The list (<see cref="LinearWorkViewModel"/>, continued): both groups read with the filters, the lookups the filter bar offers, Show more, and the states of the last read.</summary>
internal sealed partial class LinearWorkViewModel
{
    private int _reads;
    private LinearIssueList? _list;   // the latest "mine" answer: it carries the feed, the viewer and whether Linear is connected
    private string? _mineNext;
    private string? _teamsNext;
    private DateTimeOffset? _lastGood;

    /// <summary>True until the first answer (skeleton rows once, never on a refresh).</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>Assigned to you: open, by priority then last updated.</summary>
    public LinearGroupViewModel Mine { get; }

    /// <summary>Your teams: the profile teams' open issues not assigned to you.</summary>
    public LinearGroupViewModel Teams { get; }

    /// <summary>The two groups in order.</summary>
    public ObservableCollection<LinearGroupViewModel> Groups { get; }

    /// <summary>The seven drop-downs, in the bar's order.</summary>
    public ObservableCollection<LinearFilterViewModel> Filters { get; }

    /// <summary>A filter by key.</summary>
    /// <param name="key">team, project, cycle, state, assignee, priority or label.</param>
    /// <returns>The filter.</returns>
    internal LinearFilterViewModel Filter(string key) => Filters.First(f => string.Equals(f.Key, key, StringComparison.Ordinal));

    /// <summary>True when any filter is on.</summary>
    public bool HasFilters => Filters.Any(f => f.Value is not null);

    /// <summary>True when a group has another page.</summary>
    public bool HasMore => _mineNext is not null || _teamsNext is not null;

    /// <summary>True when no Linear source is set up: the Connect Linear card replaces the list.</summary>
    public bool ShowsConnect => _list is { Connected: false };

    /// <summary>True while the viewer step has not run and nothing is assigned yet.</summary>
    public bool ShowsFinding => _list is { Connected: true, Viewer: null } && Mine.Rows.Count == 0;

    /// <summary>True when filters are on and nothing matches.</summary>
    public bool ShowsNoMatch => _list is { Connected: true, Viewer: not null } && !IsLoading && HasFilters && Mine.Rows.Count == 0 && Teams.Rows.Count == 0;

    /// <summary>"Updating issues… 412 of 1,000" while the feed re-reads; empty otherwise.</summary>
    public string UpdatingText => _list is { } list && string.Equals(list.Feed.State, "rereading", StringComparison.Ordinal) ? Labels.LinearUpdating(list.Feed.Reread) : string.Empty;

    /// <summary>"Linear sync failing since 22:05 (SND-LINEAR-010)" while the feed fails; empty otherwise.</summary>
    public string FeedText => _list is { } list && string.Equals(list.Feed.State, "failing", StringComparison.Ordinal)
        ? Labels.LinearFeedFailing(list.Feed.LastSyncedAt is { } at ? Clock(at) : string.Empty, list.Feed.Code ?? string.Empty)
        : string.Empty;

    /// <summary>True when the last read could not reach the API (the rows shown are the last good ones).</summary>
    public bool IsSindriDown { get; private set => Set(ref field, value); }

    /// <summary>"as of 10:05" while Sindri is not answering and rows are shown; empty otherwise.</summary>
    public string AsOfText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Focus should move to a row (Show more: the first new one).</summary>
    internal event EventHandler<FocusRequestedEventArgs>? FocusRequested;

    /// <summary>The Connect Linear button was pressed: the shell opens Settings.</summary>
    internal event EventHandler? ConnectRequested;

    /// <summary>A row or a strip row asked for its issue: the shell opens its sheet.</summary>
    internal event EventHandler<LinearIssueRequestedEventArgs>? IssueRequested;

    /// <summary>Asks the shell to open Settings, where the key and the teams are set.</summary>
    internal void RequestConnect() => ConnectRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the shell to open an issue's sheet.</summary>
    /// <param name="key">The issue key.</param>
    internal void RequestIssue(string key) => IssueRequested?.Invoke(this, new LinearIssueRequestedEventArgs(key));

    /// <summary>Turns every filter off and reads again, once.</summary>
    internal void ClearFilters()
    {
        foreach (var filter in Filters)
        {
            filter.Reset();
        }

        OnChanged(nameof(HasFilters));
        _selectWhenShown = null;
        Reload();
    }

    /// <summary>Loads the next page of the first group that has one, after its rows, and moves focus to the first new row.</summary>
    /// <returns>A task that completes when the rows are added.</returns>
    internal async Task ShowMore()
    {
        if (_url is not { } url || NextPage() is not var (mine, cursor))
        {
            return;
        }

        var read = _reads;   // a full read that starts meanwhile has the newer answer
        var token = _shown?.Token ?? CancellationToken.None;
        var page = await _api.LinearIssues(url, Query(mine ? "mine" : "teams", cursor), token).ConfigureAwait(true);
        if (read != _reads)
        {
            return;
        }

        if (page.IsError)
        {
            SindriNotAnswering();
            return;
        }

        Added(mine, page.Value);
    }

    // The group whose next page "Show more" loads: Assigned to you first, then Your teams; null when neither has one.
    private (bool Mine, string Cursor)? NextPage() =>
        _mineNext is { } mine ? (true, mine) : _teamsNext is { } teams ? (false, teams) : null;

    private void Added(bool mine, LinearIssueList page)
    {
        var added = page.Rows.Select(Row).ToList();
        (mine ? Mine : Teams).Append(added, page.Total);
        (_mineNext, _teamsNext) = mine ? (page.Next, _teamsNext) : (_mineNext, page.Next);
        OnChanged(nameof(HasMore));
        if (added.Count > 0)
        {
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(added[0].Key, onlyIfLost: false));
        }
    }

    private void OnFilterPicked(object? sender, EventArgs e)
    {
        OnChanged(nameof(HasFilters));
        _selectWhenShown = null;   // the person chose what the list shows: a created issue is no longer owed a place in it
        Reload();
    }

    // A filter changed: read both groups again from the first page; the rows stay until the answer.
    private void Reload()
    {
        if (IsLinear && IsActive && _url is { } url)
        {
            Reading = Observed(ReadList(url, _shown?.Token ?? CancellationToken.None, keepLoaded: false));
        }
    }

    // The most rows a refresh asks for per group, so it keeps what "Show more" loaded without growing without bound.
    private const int RefreshCap = 200;

    private LinearIssueQuery Query(string group, string? after, int limit = PageSize) => new(
        group,
        Team: Filter("team").Value,
        Project: Filter("project").Value,
        Cycle: Filter("cycle").Value,
        State: Filter("state").Value,
        Assignee: Filter("assignee").Value,
        Priority: int.TryParse(Filter("priority").Value, NumberStyles.None, CultureInfo.InvariantCulture, out var priority) ? priority : null,
        Label: Filter("label").Value,
        After: after,
        Limit: limit);

    // A refresh asks for as many rows as the group shows (up to the cap), so an event never folds "Show more" back to the first page and the cursor stays right;
    // a filter change starts from the first page.
    private async Task ReadList(Uri url, CancellationToken cancellationToken, bool keepLoaded = true)
    {
        var read = ++_reads;
        var mineRead = _api.LinearIssues(url, Query("mine", after: null, Loaded(Mine, keepLoaded)), cancellationToken);
        var teamsRead = _api.LinearIssues(url, Query("teams", after: null, Loaded(Teams, keepLoaded)), cancellationToken);
        var (mine, teams) = (await mineRead.ConfigureAwait(true), await teamsRead.ConfigureAwait(true));
        if (read != _reads)
        {
            return;   // a newer read was asked for since, or Work was left: its answer is the one to show
        }

        if (mine.IsError || teams.IsError)
        {
            SindriNotAnswering();
            return;
        }

        Show(mine.Value, teams.Value);
        await Lookups(url, cancellationToken).ConfigureAwait(true);
    }

    private static int Loaded(LinearGroupViewModel group, bool keep) => keep ? Math.Clamp(group.Rows.Count, PageSize, RefreshCap) : PageSize;

    // Both groups from one pair of answers; the rows already shown are updated in place.
    private void Show(LinearIssueList mine, LinearIssueList teams)
    {
        _list = mine;
        _lastGood = _clock.GetUtcNow();
        (IsSindriDown, AsOfText) = (false, string.Empty);
        var kept = new Dictionary<string, LinearIssueRowViewModel>(StringComparer.Ordinal);
        var mineRows = mine.Rows.Select(r => Row(r, kept)).ToList();
        var teamRows = teams.Rows.Select(r => Row(r, kept)).ToList();
        _rows.Clear();
        foreach (var (key, row) in kept)
        {
            _rows[key] = row;
        }

        Mine.Show(mineRows, mine.Total, MineEmpty(mine));
        Teams.Show(teamRows, teams.Total, string.Empty);
        KeepSelection();
        _ = SelectPending();
        (_mineNext, _teamsNext) = (mine.Next, teams.Next);
        foreach (var row in mine.Rows.Concat(teams.Rows))
        {
            OfferFrom(row);
        }

        IsLoading = false;
        foreach (var name in _derived)
        {
            OnChanged(name);
        }
    }

    // The line under "Assigned to you" while it has no row: finding the viewer, or nothing assigned; with filters on, the box says it.
    private string MineEmpty(LinearIssueList mine) =>
        !mine.Connected ? string.Empty : mine.Viewer is null ? Labels.LinearFinding : HasFilters ? string.Empty : Labels.LinearNoneAssigned;

    private LinearIssueRowViewModel Row(LinearIssueRow served) => Row(served, _rows);

    private LinearIssueRowViewModel Row(LinearIssueRow served, Dictionary<string, LinearIssueRowViewModel> into)
    {
        if (_rows.TryGetValue(served.Key, out var shown))
        {
            shown.Update(served);
        }
        else
        {
            shown = new LinearIssueRowViewModel(served);
        }

        into[served.Key] = shown;
        return shown;
    }

    // The last read failed: the rows stay, marked with the time of the last good read.
    private void SindriNotAnswering()
    {
        IsLoading = false;
        IsSindriDown = true;
        AsOfText = _list is not null && _lastGood is { } at ? Labels.LinearAsOf(Clock(at)) : string.Empty;
        OnChanged(nameof(IsLoading));
    }

    private string Clock(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, _clock.LocalTimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);
}
