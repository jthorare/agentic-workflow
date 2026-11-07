using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The Work page's Linear view (Linear hub spec §3): the Sindri / Linear switch and its remembered pick, the owner's issues in two groups
/// (Assigned to you, then Your teams) with the filter bar and "Show more", and the since-you-looked strip. The switch and the strip's count
/// are read while Work is shown on either view; the list only on the Linear one. Everything the page says about the feed (not connected,
/// finding the viewer, re-reading, failing, unreachable) is a state of the last read, and the rows stay through a failing one.
/// </summary>
internal sealed partial class LinearWorkViewModel : ObservableObject, IDisposable
{
    // The spec's page size.
    internal const int PageSize = 50;

    private static readonly string[] _filterKeys = ["team", "project", "cycle", "state", "assignee", "priority", "label"];

    private static readonly string[] _derived =
    [
        nameof(IsLoading), nameof(ShowsConnect), nameof(ShowsFinding), nameof(ShowsNoMatch), nameof(UpdatingText), nameof(FeedText), nameof(IsUnreachable), nameof(AsOfText),
        nameof(HasMore), nameof(HasFilters),
    ];

    private readonly ISindriApi _api;
    private readonly Preferences _preferences;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, LinearIssueRowViewModel> _rows = new(StringComparer.Ordinal);
    private readonly HashSet<string> _lookedUp = new(StringComparer.Ordinal);
    private CancellationTokenSource? _shown;
    private Uri? _url;

    internal LinearWorkViewModel(ISindriApi api, Preferences preferences, TimeProvider clock)
    {
        (_api, _preferences, _clock) = (api, preferences, clock);
        Mine = new LinearGroupViewModel(Labels.LinearAssigned);
        Teams = new LinearGroupViewModel(Labels.LinearTeams);
        Groups = [Mine, Teams];
        Filters = [.. _filterKeys.Select(k => new LinearFilterViewModel(k))];
        foreach (var filter in Filters)
        {
            filter.Picked += OnFilterPicked;
        }

        foreach (var priority in new[] { 1, 2, 3, 4, 0 })
        {
            Filter("priority").Offer(priority.ToString(System.Globalization.CultureInfo.InvariantCulture), Labels.LinearPriority(priority));
        }
    }

    /// <summary>True when the Linear tab is picked.</summary>
    public bool IsLinear { get; private set; }

    /// <summary>True when the Sindri tab is picked.</summary>
    public bool IsSindri => !IsLinear;

    /// <summary>The Sindri tab's text.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string SindriTabText => Labels.WorkViewSindri;

    /// <summary>The switch's accessible name.</summary>
    public string SwitchName => Labels.WorkViewSwitch;
#pragma warning restore CA1822, MA0041

    /// <summary>The Linear tab's text: "Linear · 3 new" when the strip has changes.</summary>
    public string LinearTabText => Labels.LinearTab(NewCount);

    /// <summary>The Linear tab's accessible name: "Linear, 3 new".</summary>
    public string LinearTabName => Labels.LinearTabName(NewCount);

    /// <summary>The header button's words: "Create item" on the Sindri view, "New issue" on the Linear one.</summary>
    public string CreateText => IsLinear ? Labels.LinearNewIssue : Labels.Action("CreateItem");

    /// <summary>True while Work is the page shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>The latest reads started (the list, the strip, or both).</summary>
    internal Task Reading { get; private set; } = Task.CompletedTask;

    /// <summary>The read made when Work was shown, awaited so nothing about it is unobserved.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>Cancels the reads Work being shown started.</summary>
    public void Dispose()
    {
        _shown?.Cancel();
        _shown?.Dispose();
        _shown = null;
    }

    /// <summary>Picks a tab and remembers the pick; picking Linear while Work is shown reads the list.</summary>
    /// <param name="linear">True for Linear.</param>
    internal void Choose(bool linear)
    {
        if (IsLinear == linear)
        {
            return;
        }

        IsLinear = linear;
        foreach (var name in new[] { nameof(IsLinear), nameof(IsSindri), nameof(CreateText), nameof(ShowsStrip) })
        {
            OnChanged(name);
        }

        _ = _preferences.Update(p => p with { WorkView = linear ? UiPreferences.LinearView : UiPreferences.SindriView });
        if (linear && IsActive && _url is { } url)
        {
            Reading = Observed(ReadList(url, _shown?.Token ?? CancellationToken.None));
        }
    }

    /// <summary>`L`: the other tab.</summary>
    internal void Toggle() => Choose(!IsLinear);

    /// <summary>A key on a tab: Left and Right move to the other tab, Home to the first, End to the last.</summary>
    /// <param name="key">Left, Right, Home or End.</param>
    /// <returns>True when it is a tab key.</returns>
    internal bool MoveTab(string key)
    {
        switch (key)
        {
            case "Left" or "Right":
                Toggle();
                return true;
            case "Home":
                Choose(linear: false);
                return true;
            case "End":
                Choose(linear: true);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Reads the remembered pick and the strip's marker.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are applied.</returns>
    internal async Task Restore(CancellationToken cancellationToken)
    {
        UiPreferences stored;
        try
        {
            stored = await _preferences.Load(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;   // the defaults stand: the Sindri view and no marker
        }

        _marker = stored.LinearSince;
        if (stored.WorkView == UiPreferences.LinearView && !IsLinear)
        {
            IsLinear = true;
            foreach (var name in new[] { nameof(IsLinear), nameof(IsSindri), nameof(CreateText), nameof(ShowsStrip) })
            {
                OnChanged(name);
            }
        }
    }

    /// <summary>Work was shown or hidden; becoming the page shown reads at once.</summary>
    /// <param name="shown">True while Work is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            _shown?.Dispose();
            _shown = new CancellationTokenSource();
            CatchUp = Reading = Observed(CatchUpRead(url, _shown.Token));
        }
        else if (!shown)
        {
            _reads++;   // a read on its way is not wanted
            _sinceReads++;
            _shown?.Cancel();
        }
    }

    /// <summary>Re-reads on connect and on every event while Work is shown; otherwise it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the reads are done.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return IsActive ? Reading = ReadAll(url, cancellationToken) : Task.CompletedTask;
    }

    private async Task CatchUpRead(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await ReadAll(url, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Work was left
        }
    }

    // A read started from a key or a pick has nobody to await it: it ends in a state of the view, and a leave cancels it.
    private static async Task Observed(Task read)
    {
        try
        {
            await read.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Work was left
        }
    }

    // The strip's count is read on either view (the tab carries it); the list only on the Linear one.
    private Task ReadAll(Uri url, CancellationToken cancellationToken) =>
        Task.WhenAll(ReadSince(url, cancellationToken), IsLinear ? ReadList(url, cancellationToken) : Task.CompletedTask);
}
