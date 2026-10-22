using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page (collector spec CU-1, screens §4.5): the live agent sessions the collector sees, in the groups the API's fields give them,
/// with the recently ended under a fold. Counts, the empty sentence and the problems are the API's; the app groups the rows, keeps them by ref
/// across reads and maps fields to words. Reads happen while the page is the one shown (events, and a 5 s poll); the sheet is another view model.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="clipboard">Where Copy puts text.</param>
/// <param name="clock">The clock the poll and the ages run on.</param>
/// <param name="catalog">The command catalog, for the Focus row.</param>
internal sealed partial class AgentsPageViewModel(ISindriApi api, IClipboard clipboard, TimeProvider clock, CatalogViewModel catalog) : ObservableObject
{
    private readonly Dictionary<string, SessionRowViewModel> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EndedSessionViewModel> _ended = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SessionGroupViewModel> _groups = new(StringComparer.Ordinal);
    private readonly UiContext _ui = new();
    private readonly TimeProvider _clock = clock;
    private SessionList? _list;
    private (int Live, int Asking)? _counts;
    private int _reads;

    /// <summary>Raised when a row asks to open its session's sheet; the shell's session-sheet partial handles it.</summary>
    internal event EventHandler<SessionOpenRequestedEventArgs>? OpenRequested;

    /// <summary>Raised when a row asks to open the frame of the Step its session works on; the shell opens it.</summary>
    internal event EventHandler<SessionFrameRequestedEventArgs>? FrameRequested;

    /// <summary>Raised when the page should put focus on a row or its Focus line.</summary>
    internal event EventHandler<AgentFocusEventArgs>? FocusRequested;

    /// <summary>The API the page reads.</summary>
    internal ISindriApi Api { get; } = api;

    /// <summary>Where Copy puts text.</summary>
    internal IClipboard Clipboard { get; } = clipboard;

    /// <summary>The time on the page's clock (the poll and the ages run on it).</summary>
    internal DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>The text of the page's tab count; empty before the first status. The counts partial fills it.</summary>
    public string NavCount { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The groups: "Started by Sindri" when Sindri launched any, then "Not part of a workflow".</summary>
    public ObservableCollection<SessionGroupViewModel> Groups { get; } = [];

    /// <summary>The recently ended sessions, under the fold.</summary>
    public ObservableCollection<EndedSessionViewModel> Ended { get; } = [];

    /// <summary>One line per source problem, "code text" as served, above the list.</summary>
    public ObservableCollection<string> ProblemLines { get; } = [];

    /// <summary>"3 live · 1 asking · 2 outside a workflow", as served and whatever the filters; empty until the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True once a list has been read.</summary>
    public bool HasHead => Head.Length > 0;

    /// <summary>The head's accessible name.</summary>
    public string HeadName => HasHead ? Labels.Agents + ", " + Head : Labels.Agents;

    /// <summary>The page's footer: what the list can't show.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Footer => Labels.CursorChatsNotShown;
#pragma warning restore CA1822, MA0041

    /// <summary>"Reading sessions…" until the first answer.</summary>
    public string ReadingText { get; private set => Set(ref field, value); } = Labels.ReadingSessions;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set { Set(ref field, value); OnChanged(nameof(ReadingText)); } } = true;

    /// <summary>The line that replaces the list when the collector can't run here (SND-SESSION-002); empty otherwise.</summary>
    public string UnavailableText { get; private set { Set(ref field, value); OnChanged(nameof(IsUnavailable)); OnChanged(nameof(FiltersEnabled)); } } = string.Empty;

    /// <summary>True while the collector can't run here.</summary>
    public bool IsUnavailable => UnavailableText.Length > 0;

    /// <summary>True when the filters can be used.</summary>
    public bool FiltersEnabled => !IsUnavailable && !IsReading;

    /// <summary>True when the list is empty or no row matches the filters.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The served empty sentence; the app's own when a filter leaves no row; else empty.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>What the page says then.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string UnreadableText => Labels.SessionsUnreadable;

    /// <summary>The column captions above the rows.</summary>
    public AgentColumns Columns { get; } = new(Labels.ColumnStatus, Labels.ColumnAgent, Labels.ColumnWhere, Labels.ColumnDoing, Labels.ColumnFor);

    /// <summary>True when there are rows to put the captions over.</summary>
    public bool HasColumns => Groups.Count > 0;

    /// <summary>The stale note's words.</summary>
    public string StaleText => Labels.Stale;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the rows shown are the last good ones (a read failed).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>The fold's header with the count; empty when nothing ended.</summary>
    public string EndedHeader => Ended.Count == 0 ? string.Empty : Labels.RecentlyEndedHeader(Ended.Count);

    /// <summary>True when something ended recently.</summary>
    public bool HasEnded => Ended.Count > 0;

    /// <summary>True while the ended rows show.</summary>
    public bool IsEndedExpanded { get; private set { Set(ref field, value); OnChanged(nameof(EndedToggleName)); } }

    /// <summary>The fold button's accessible name.</summary>
    public string EndedToggleName => EndedHeader + ", " + (IsEndedExpanded ? Labels.GroupExpanded : Labels.GroupCollapsed);

    /// <summary>The live region that says "N agents · M asking" when those counts change; never on a read that changes nothing.</summary>
    public LiveAnnouncer Voice { get; } = new(Labels.Agents);

    /// <summary>How many lists this page has drawn (a test waits on it).</summary>
    internal int Drawn { get; private set; }

    /// <summary>Re-reads the list (on connect and on every event). While the page is not the one shown it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        Url = url;
        return IsActive ? Read(url, cancellationToken) : Task.CompletedTask;
    }

    private Uri? Url { get; set; }

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await Api.Sessions(url, new SessionQuery(), cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
        }
        else if (string.Equals(list.Error.Code, "SND-SESSION-002", StringComparison.Ordinal))
        {
            Unavailable(list.Error.Message);
        }
        else if (string.Equals(list.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            (IsReading, IsUnreadable, IsStale) = (false, true, true);
            OnChanged(nameof(FiltersEnabled));
        }
        else
        {
            (IsReading, IsStale) = (false, true);   // the last good rows stay, marked stale (screens §4.1)
            OnChanged(nameof(FiltersEnabled));
        }
    }

    private void Show(SessionList list)
    {
        Drawn++;
        var before = WalkKeys();
        (_list, IsReading, IsUnreadable, IsStale) = (list, false, false, false);
        UnavailableText = string.Empty;
        Head = Labels.AgentsHead(list.Counts.Live, list.Counts.Asking);
        OnChanged(nameof(HasHead));
        OnChanged(nameof(HeadName));
        ObserveCounts(list.Counts.Live, list.Counts.Asking);
        Patch.To(ProblemLines, [.. list.Problems.Select(p => p.Code + " " + p.Text)]);
        ReplaceChoices(list);
        Apply(before, list);
    }

    private void Unavailable(string detail)
    {
        Drawn++;
        (_list, _counts) = (null, null);
        (IsReading, IsUnreadable, IsStale) = (false, false, false);
        Head = string.Empty;
        OnChanged(nameof(HasHead));
        OnChanged(nameof(HeadName));
        UnavailableText = Labels.SessionsUnavailable(detail);
        ProblemLines.Clear();
        Groups.Clear();
        _groups.Clear();
        _rows.Clear();
        Ended.Clear();
        _ended.Clear();
        (SelectedKey, IsEmpty, EmptyText) = (null, false, string.Empty);
        OnChanged(nameof(EndedHeader));
        OnChanged(nameof(HasEnded));
        OnChanged(nameof(EndedToggleName));
    }

    /// <summary>
    /// Notes the live and asking figures (from the list, or from the status the shell holds): "N agents · M asking" is said when they changed since the
    /// last figures, never for the first ones or a read that changes nothing, and only while the page is the one shown.
    /// </summary>
    /// <param name="live">Live sessions.</param>
    /// <param name="asking">Sessions waiting on the human.</param>
    internal void ObserveCounts(int live, int asking)
    {
        var now = (live, asking);
        if (_counts is { } was && was != now && IsActive)
        {
            Voice.Say(Labels.AgentsCounts(live, asking));
        }

        _counts = now;
    }

    // Draws the last list through the filters: rows are kept by ref, so a read never rebuilds a row (or the control with focus) that did not change.
    private void Apply(IReadOnlyList<string> before, SessionList list)
    {
        var kept = list.Sessions.Where(Matches).Select(Row).ToList();
        var live = new HashSet<string>(list.Sessions.Select(s => s.Ref), StringComparer.Ordinal);
        foreach (var gone in _rows.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _ = _rows.Remove(gone);
        }

        var outside = kept.Where(r => !r.IsLaunched).ToList();
        var launched = kept.Where(r => r.IsLaunched).ToList();
        var shown = new List<SessionGroupViewModel>();
        if (launched.Count > 0)
        {
            shown.Add(Group(Labels.StartedBySindri, launched));
        }

        if (outside.Count > 0)
        {
            shown.Add(Group(Labels.OutsideWorkflow, outside));
        }

        Patch.To(Groups, shown);
        OnChanged(nameof(HasColumns));
        ShowEnded(list);
        IsEmpty = kept.Count == 0;
        EmptyText = IsEmpty ? (list.Sessions.Count == 0 ? list.Empty ?? Labels.NoSessionsMatch : Labels.NoSessionsMatch) : string.Empty;
        Reselect(before);
    }

    /// <summary>Expands the recently ended fold, or collapses it.</summary>
    internal void ToggleEnded() => IsEndedExpanded = !IsEndedExpanded;

    /// <summary>Asks the shell to open a session's sheet.</summary>
    /// <param name="args">The link.</param>
    internal void RequestOpen(SessionOpenRequestedEventArgs args) => OpenRequested?.Invoke(this, args);

    private void RequestFocus(AgentFocusEventArgs args) => FocusRequested?.Invoke(this, args);
}
