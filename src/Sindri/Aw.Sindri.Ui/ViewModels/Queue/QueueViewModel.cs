using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// GET /sindri/queue as the Queue page (UI spec §4.5) and Home's Running and With you and Queued panels (§3.2, §4.2).
/// Groups, their order, the rows' order, the empty sentences and the actions are the API's (§2 "no logic"); the app maps
/// them to words and visibility, caps Home's panels at five rows, and keeps the selected row by id across refreshes (§3.3).
/// </summary>
internal sealed partial class QueueViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock) : ObservableObject
{
    // Home's panels show at most this many rows, then "Show all N in Queue" (§3.2 caps).
    internal const int PanelCap = 5;

    private QueueView? _view;
    private QueueScope _scope;   // the rows' page: the one the shell shows (PageShown), else where the selection was last made (Select, J / K)
    private bool _onPage = true;   // false while the shell shows a page without rows

    // The rows and groups as last shown, by id and by name: a refresh updates them in place (§3.3).
    private Dictionary<string, ItemRowViewModel> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QueueGroupViewModel> _groups = new(StringComparer.Ordinal);

    /// <summary>The groups in the API's order (empty ones included, hidden by <see cref="QueueGroupViewModel.IsVisible"/>).</summary>
    public ObservableCollection<QueueGroupViewModel> Groups { get; } = [];

    /// <summary>How many items the queue holds across its groups (the Queue tab's count).</summary>
    public int Total { get; private set => Set(ref field, value); }

    /// <summary>True when no group has a row and no filter is on: the page shows the view's sentence.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The view's own empty sentence, as served.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True until the first answer (§4.1: skeleton rows once, never on refresh).</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the last read failed: the rows shown are the last good ones (§4.1 Stale).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True while the first read's failure is shown (nothing was ever read, so there are no rows to mark stale).</summary>
    public bool HasProblem => ProblemCode.Length > 0;

    /// <summary>The failed first read's code (the API's title, or the app's SND-UI code), or empty.</summary>
    public string ProblemCode { get; private set { Set(ref field, value); OnChanged(nameof(HasProblem)); } } = string.Empty;

    /// <summary>The failed first read's detail, as served, or empty.</summary>
    public string ProblemDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Home's left panel: With you first, then running, at most five (§3.2).</summary>
    public ObservableCollection<ItemRowViewModel> RunningRows { get; } = [];

    /// <summary>"RUNNING N · WITH YOU N".</summary>
    public string RunningHeader { get; private set => Set(ref field, value); } = Labels.RunningHeader(0, 0);

    /// <summary>The running group's sentence while the panel has no row (E-18), else empty.</summary>
    public string RunningEmpty { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the panel holds more rows than it shows.</summary>
    public bool HasMoreRunning { get; private set => Set(ref field, value); }

    /// <summary>"Show all N in Queue" for the left panel.</summary>
    public string ShowAllRunning { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Home's right panel: the queued group's first five rows.</summary>
    public ObservableCollection<ItemRowViewModel> QueuedRows { get; } = [];

    /// <summary>"QUEUED N".</summary>
    public string QueuedHeader { get; private set => Set(ref field, value); } = Labels.QueuedHeader(0);

    /// <summary>The queued group's sentence while it has no row (E-18), else empty.</summary>
    public string QueuedEmpty { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the queued group holds more rows than the panel shows.</summary>
    public bool HasMoreQueued { get; private set => Set(ref field, value); }

    /// <summary>"Show all N in Queue" for the right panel.</summary>
    public string ShowAllQueued { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The selected row's id, kept across refreshes; null when none.</summary>
    public string? SelectedId { get; private set => Set(ref field, value); }

    /// <summary>
    /// The polite region's text: "ITM-… cleared" (§3.3) or "N new" while it is live, else its neutral resting text (never
    /// empty). The region is live only while <see cref="IsAnnouncing"/>, which changes before the text.
    /// </summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The polite region's gate: true while it is live (set before its text changes).</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>
    /// Raised when focus should follow the selection onto a row: J / K, and a refresh that moved the selection because its
    /// row cleared (§3.3). The view moves focus; the view model never touches a control.
    /// </summary>
    internal event EventHandler<FocusRequestedEventArgs>? FocusRequested;

    /// <summary>The filter the page shows (all of it, or Running from Home's "Show all N in Queue").</summary>
    public QueueFilter Showing { get; private set; }

    /// <summary>Re-reads the view (on connect and on every event, §9.2) and shows it; a failure keeps the last rows, stale.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the view is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        LastUrl = url;
        var answer = await api.Queue(url, cancellationToken).ConfigureAwait(true);
        if (answer.IsError)
        {
            // Rows were shown: they stay, marked stale. Nothing was ever read: say what the API said instead of a skeleton for good.
            // The problem first: whoever waits on the end of loading finds it there.
            if (_view is null)
            {
                (ProblemCode, ProblemDetail) = (answer.Error.Code, answer.Error.Message);
            }

            (IsStale, IsLoading) = (_view is not null, false);
            return;
        }

        (IsStale, ProblemCode, ProblemDetail) = (false, string.Empty, string.Empty);
        Rest();
        Apply(Hold(answer.Value));
        IsLoading = false;   // after the rows are there: a waiter on the end of loading finds them
    }

    /// <summary>Shows the running group (Home's "Show all N in Queue") or all of them (G Q, the Work tab); a selection the filter hides is cleared, silently.</summary>
    /// <param name="filter">The filter.</param>
    internal void Filter(QueueFilter filter)
    {
        Showing = filter;
        Show();
        ClearHidden();
    }

    // Shows a view, keeping the selection by id (§3.3).
    private void Apply(QueueView view, bool byPill = false)
    {
        var before = Navigable(_scope).Select(r => (r.Id, r.Group)).ToList();
        _view = view;
        Show();
        Keep(before, byPill);
        if (_spoken is { } text)
        {
            _spoken = null;
            Say(text);
        }
    }

    private IEnumerable<ItemRowViewModel> Visible() => Groups.Where(g => g.IsVisible).SelectMany(g => g.Rows);

    private void Show()
    {
        if (_view is not { } view)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var fresh = new Dictionary<string, ItemRowViewModel>(StringComparer.Ordinal);
        var all = view.Groups.ToDictionary(g => g.Group, g => g.Items.Select(i => Row(i, g.Group, now, fresh)).ToList(), StringComparer.Ordinal);
        _rows = fresh;
        Patch.To(Groups, [.. view.Groups.Select(g => Group(g, all[g.Group]))]);
        Total = all.Values.Sum(r => r.Count);
        var unmatched = (FilterText.Length > 0 || SelectedRepo.Length > 0) && !Visible().Any();
        IsEmpty = (Showing == QueueFilter.All && Total == 0) || unmatched;
        EmptyText = unmatched ? Labels.NoMatches : IsEmpty ? view.Empty ?? string.Empty : string.Empty;
        Panels(all);
        Toolbar(all);
        States(view);
        Mark();
    }

    // The row for an item: the one already shown, updated, or a new one.
    private ItemRowViewModel Row(ItemSummaryView item, string group, DateTimeOffset now, Dictionary<string, ItemRowViewModel> fresh)
    {
        var action = Primary(item);
        if (_rows.TryGetValue(item.Id, out var row))
        {
            row.Update(item, group, action, now);
        }
        else
        {
            row = new ItemRowViewModel(item, group, action, now);
        }

        fresh[item.Id] = row;
        return row;
    }

    // The filter lets a group through (Running from Home's link; all of them otherwise).
    private bool Picked(string group) => Showing switch
    {
        QueueFilter.Running => group == "running",
        QueueFilter.All => true,
        _ => true,
    };

    private QueueGroupViewModel Group(QueueGroupView group, List<ItemRowViewModel> all)
    {
        var picked = Picked(group.Group);
        if (!_groups.TryGetValue(group.Group, out var shown))
        {
            shown = _groups[group.Group] = new QueueGroupViewModel(group.Group);
        }

        shown.Update([.. all.Where(r => Matches(r.Id, r.Title, r.Repo))], EmptyLine(group), picked, filtered: picked && Showing != QueueFilter.All);
        return shown;
    }

    // The row's primary action as the catalog's control. A row sends nothing itself: its item actions need the version the
    // sheet shows (CloseItem) or arrive in later phases, so the control is drawn (pending with its phase) and Enter opens the sheet.
    private ControlViewModel? Primary(ItemSummaryView item) =>
        item.PrimaryAction is { } action ? catalog.On(action, item.Id, send: null) : null;

    // Home's panels show the whole groups: the Queue page's filter doesn't apply to them.
    private void Panels(Dictionary<string, List<ItemRowViewModel>> all)
    {
        var withYou = all.GetValueOrDefault("withYou") ?? [];
        var running = all.GetValueOrDefault("running") ?? [];
        var queued = all.GetValueOrDefault("queued") ?? [];
        var left = withYou.Concat(running).ToList();
        Patch.To(RunningRows, [.. left.Take(PanelCap)]);
        RunningHeader = Labels.RunningHeader(running.Count, withYou.Count);
        RunningEmpty = left.Count == 0 ? EmptyOf("running") : string.Empty;
        (HasMoreRunning, ShowAllRunning) = (left.Count > PanelCap, Labels.ShowAllInQueue(left.Count));
        Patch.To(QueuedRows, [.. queued.Take(PanelCap)]);
        QueuedHeader = Labels.QueuedHeader(queued.Count);
        QueuedEmpty = queued.Count == 0 ? EmptyOf("queued") : string.Empty;
        (HasMoreQueued, ShowAllQueued) = (queued.Count > PanelCap, Labels.ShowAllInQueue(queued.Count));
    }

    private string EmptyOf(string group) => Groups.FirstOrDefault(g => g.Group == group)?.Empty ?? string.Empty;
}
