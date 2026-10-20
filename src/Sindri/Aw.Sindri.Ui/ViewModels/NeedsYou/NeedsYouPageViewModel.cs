using System.Collections.ObjectModel;
using System.ComponentModel;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The Needs-you inbox page (F3 needs-you spec §2): the rows grouped by kind, and one request card for the selected row. It
/// reads the list Home's panel reads and shares its selection, so a row selected on one is selected on the other; it lists every
/// row (Home caps its panel), the app never sorts or filters them, and a row keeps its place in its group's served order.
/// </summary>
internal sealed class NeedsYouPageViewModel : ObservableObject
{
    private readonly NeedsYouViewModel _list;
    private readonly TimeProvider _clock;
    private readonly IClipboard _clipboard;
    private NeedsYouRowViewModel? _lastRow;   // the row the card last showed as selected
    private NeedsYouRowViewModel? _cleared;   // that row, once it left the list under the card, until the user moves on

    internal NeedsYouPageViewModel(NeedsYouViewModel list, TimeProvider clock, IClipboard clipboard)
    {
        _list = list;
        _clock = clock;
        _clipboard = clipboard;
        _list.PropertyChanged += OnListChanged;
        Rebuild();
        _lastRow = Selected;
    }

    /// <summary>Raised when the breadcrumb asks to go back to Home.</summary>
    internal event EventHandler? LeaveRequested;

    /// <summary>Raised when the card's link asks to open the item the request is about.</summary>
    internal event EventHandler<NeedsYouRowEventArgs>? ItemRequested;

    /// <summary>The groups, in the page's order.</summary>
    public ObservableCollection<NeedsYouInboxGroup> Groups { get; } = [];

    /// <summary>The title.</summary>
    public string Title { get; } = Labels.NeedsYouInboxTitle;

    /// <summary>The breadcrumb's first word, a button back to Home.</summary>
    public string CrumbHome { get; } = Labels.NeedsYouCrumbHome;

    /// <summary>The breadcrumb's separator.</summary>
    public string CrumbSeparator { get; } = Labels.NeedsYouCrumbSeparator;

    /// <summary>The breadcrumb button's accessible name.</summary>
    public string CrumbHomeName { get; } = Labels.NeedsYouCrumbHomeName;

    /// <summary>The key hint's word for J and K.</summary>
    public string KeyMoveWord { get; } = Labels.NeedsYouKeyMoveWord;

    /// <summary>The caption of the card's first section.</summary>
    public string WhyCaption { get; } = Labels.NeedsYouWhyCaption;

    /// <summary>The caption of the section that says what a decision asks.</summary>
    public string RequestCaption { get; } = Labels.NeedsYouRequestCaption;

    /// <summary>The link from the card's head to the item.</summary>
    public string OpenItemWord { get; } = Labels.NeedsYouOpenItem;

    /// <summary>The copy button's accessible name.</summary>
    public string CopyReferenceWord { get; } = Labels.NeedsYouCopyReference;

    /// <summary>The copy button's visible word.</summary>
    public string CopyWord { get; } = Labels.Copy;

    /// <summary>The header's count: "3 waiting, 1 runs".</summary>
    public string CountText => Labels.NeedsYouWaiting(_list.Seen.Rows.Count, _list.Seen.Rows.Count(r => NeedsYouGrouping.KindOf(r.Entry) == NeedsYouGroupKind.Runs));

    /// <summary>True while the stalled or rebuilding banner shows.</summary>
    public bool HasBanner => _list.HasBanner;

    /// <summary>The banner's served sentence.</summary>
    public string Banner => _list.Banner;

    /// <summary>True when new rows are held back while the pointer is over the list.</summary>
    public bool HasHeld => _list.HasHeld;

    /// <summary>The held-rows pill's words.</summary>
    public string HeldText => HasHeld ? Labels.NeedsYouNewPill(_list.HeldCount) : "";

    /// <summary>Shows the held rows now.</summary>
    internal void ShowHeld() => _list.ShowHeld();

    /// <summary>True while the pointer is over the inbox's list: new rows wait behind the pill.</summary>
    internal bool PointerOver
    {
        get => _list.PointerOver;
        set => _list.PointerOver = value;
    }

    /// <summary>True when the list has no rows.</summary>
    public bool HasNoRows => _list.Seen.Rows.Count == 0;

    /// <summary>What the detail column says when there are no rows: the served sentence, else the app's.</summary>
    public string EmptyText => _list.EmptyText.Length > 0 ? _list.EmptyText : Labels.NeedsYouEmptyFallback;

    /// <summary>The selected row; null for none.</summary>
    public NeedsYouRowViewModel? Selected => _list.SelectedId is { } id ? _list.Seen.Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal)) : null;

    /// <summary>The row the card shows: the selected one, or the one that cleared until the user moves on.</summary>
    public NeedsYouRowViewModel? Card => _cleared ?? Selected;

    /// <summary>True while the card shows a row that has cleared.</summary>
    public bool IsCleared => _cleared is not null;

    /// <summary>The cleared card's line.</summary>
    public string ClearedText => _cleared is { } row ? Labels.Cleared(row.ShownId.Length > 0 ? row.ShownId : row.Id) : "";

    /// <summary>The footer's accessible name: both its words.</summary>
    public string FooterName => FooterRight.Length > 0 ? FooterLeft + ", " + FooterRight : FooterLeft;

    /// <summary>True when a row is selected: the card shows.</summary>
    public bool HasSelected => Card is not null;

    /// <summary>The card's kind word, at the right of its head.</summary>
    public string KindWord => Card?.Entry is { } entry ? KindWordOf(entry) : "";

    /// <summary>The card's accessible name: the kind word and the title.</summary>
    public string CardName => Card is { } row ? Labels.NeedsYouCardName(KindWord, row.Title) : "";

    /// <summary>The reference the head shows in grey and copies: the id the row shows, else its own.</summary>
    public string Reference => Card is { } row ? (row.ShownId.Length > 0 ? row.ShownId : row.Id) : "";

    /// <summary>The first section: why the request needs the user, as the row serves it.</summary>
    public string Why => Card?.Entry is { } entry ? (entry.Reason.Length > 0 ? entry.Reason : entry.Label) : "";

    /// <summary>What a decision asks; empty for any other row.</summary>
    public string Question => Card?.Entry.Question ?? "";

    /// <summary>The card's footer, left: what the row waits for.</summary>
    public string FooterLeft { get; } = Labels.NeedsYouFootWaiting;

    /// <summary>The card's footer, right: how long it has waited; empty when the row serves no time.</summary>
    public string FooterRight => Card?.Entry is { Since: var since } && since != default ? Labels.NeedsYouFootRight(Labels.NeedsYouWaited(_clock.GetUtcNow() - since)) : "";

    /// <summary>Selects the first row of the first group when none is selected (or the selected one is gone).</summary>
    internal void SelectFirst()
    {
        if (Selected is null && Groups.Count > 0 && Groups[0].Rows.Count > 0)
        {
            _list.Select(Groups[0].Rows[0].Id);
        }
    }

    /// <summary>Moves the selection one row through the groups in the page's order.</summary>
    /// <param name="step">1 for the next row, -1 for the previous.</param>
    /// <returns>True when a row is selected after the move.</returns>
    internal bool Move(int step)
    {
        var rows = Groups.SelectMany(g => g.Rows).ToList();
        if (rows.Count == 0)
        {
            return false;
        }

        var at = rows.FindIndex(r => string.Equals(r.Id, _list.SelectedId, StringComparison.Ordinal));
        Leave(rows[at < 0 ? 0 : Math.Clamp(at + step, 0, rows.Count - 1)].Id);
        return true;
    }

    /// <summary>Starts or stops the count that marks a row seen: it counts while the row is focused on screen in the active window (the list's rule, as on Home).</summary>
    /// <param name="id">The row's id.</param>
    /// <param name="looking">True while the user looks at it.</param>
    internal void Watch(string id, bool looking) => _list.Seen.Watch(id, looking);

    /// <summary>Selects a row (a click or focus on it).</summary>
    /// <param name="id">The row's id.</param>
    internal void Select(string id) => Leave(id);

    /// <summary>Goes back to Home (the breadcrumb).</summary>
    internal void Leave() => LeaveRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Opens the item the selected request is about, when it names one.</summary>
    internal void OpenItem()
    {
        if (Selected is { CanOpenItem: true } row)
        {
            ItemRequested?.Invoke(this, new NeedsYouRowEventArgs(row.Id));
        }
    }

    /// <summary>Presses one of the selected row's controls, as its button on Home does.</summary>
    /// <param name="action">The control's action.</param>
    internal void Press(NeedsYouAction action)
    {
        if (Selected is { } row)
        {
            _list.Press(row.Id, action);
        }
    }

    /// <summary>Copies the card's reference.</summary>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes when it is copied.</returns>
    internal Task CopyReference(CancellationToken cancellationToken) => Reference.Length > 0 ? _clipboard.SetText(Reference, cancellationToken) : Task.CompletedTask;

    private static string KindWordOf(NeedsYouEntry entry) => NeedsYouGrouping.KindOf(entry) switch
    {
        NeedsYouGroupKind.Approvals => Labels.NeedsYouKindApproval,
        NeedsYouGroupKind.Runs => Labels.NeedsYouKindRun,
        NeedsYouGroupKind.Decisions => Labels.NeedsYouKindDecision,
        NeedsYouGroupKind.Settings => entry.Id.StartsWith("profile:", StringComparison.Ordinal) ? Labels.NeedsYouKindSettings : Labels.NeedsYouKindWorkflow,
        NeedsYouGroupKind.Failures => Labels.NeedsYouKindFailure,
        NeedsYouGroupKind.Other => entry.Kind,
        _ => throw new InvalidOperationException("a group the app doesn't know"),
    };

    private static readonly string[] _cardProperties =
    [
        nameof(Selected), nameof(Card), nameof(IsCleared), nameof(ClearedText), nameof(FooterName), nameof(HasSelected), nameof(KindWord), nameof(CardName), nameof(Reference), nameof(Why), nameof(Question), nameof(FooterRight),
    ];

    private static readonly string[] _listProperties =
    [
        nameof(CountText), nameof(HasBanner), nameof(Banner), nameof(HasHeld), nameof(HeldText), nameof(HasNoRows), nameof(EmptyText),
    ];

    private void OnListChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NeedsYouViewModel.Rows), StringComparison.Ordinal))
        {
            Rebuild();
            Report(_listProperties);
            Report(_cardProperties);
            if (Active && _list.SelectedId is null && _cleared is null)
            {
                SelectFirst();   // an inbox opened empty gets its first row when rows arrive
            }
        }
        else if (string.Equals(e.PropertyName, nameof(NeedsYouViewModel.SelectedId), StringComparison.Ordinal))
        {
            FollowSelection();
            Report(_cardProperties);
        }
    }

    // The user picks a row (a click, J, K): the cleared card, if any, gives way even when the pick is the row already selected.
    private void Leave(string id)
    {
        _cleared = null;
        _list.Select(id);
        _lastRow = Selected ?? _lastRow;
        Report(_cardProperties);
    }

    // The list moved the selection on its own because the row under the card left: the card keeps that row, cleared, until the user moves.
    private void FollowSelection()
    {
        if (!_list.SelectionMovedOnItsOwn)
        {
            _cleared = null;
        }
        else if (_lastRow is not null && !_list.Seen.Rows.Contains(_lastRow))
        {
            _cleared = _lastRow;
        }

        _lastRow = Selected ?? _lastRow;
    }

    /// <summary>True while the inbox shows: rows that arrive later get a selection.</summary>
    internal bool Active { get; set; }

    private void Report(string[] names)
    {
        foreach (var name in names)
        {
            OnChanged(name);
        }
    }

    // The groups are kept by (kind, heading) so a group that stays keeps its controls; its rows are the list's own row view models.
    private void Rebuild()
    {
        var split = NeedsYouGrouping.Split(_list.Seen.Rows, r => r.Entry);
        var target = new List<NeedsYouInboxGroup>(split.Count);
        foreach (var group in split)
        {
            var kept = Groups.FirstOrDefault(g => g.Kind == group.Kind && string.Equals(g.Heading, group.Heading, StringComparison.Ordinal)) ?? new NeedsYouInboxGroup(group.Kind, group.Heading);
            kept.Update(group.Rows);
            target.Add(kept);
        }

        Patch.To(Groups, target);
    }
}
