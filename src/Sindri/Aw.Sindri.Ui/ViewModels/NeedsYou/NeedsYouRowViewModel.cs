using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// How one needs-you row looks as to urgency, seen state and motion (UI spec §5; screens spec R8-13: the row's tint carries
/// what the side bar did). A pulse row is tinted and pulses until seen, then its tint clears; under reduced motion it keeps
/// a static outline and the word "new" until seen instead. A failure row keeps a steady tint, a calm row a subtle one.
/// </summary>
internal sealed partial class NeedsYouRowViewModel : ObservableObject
{
    private bool _seen;
    private bool _reduced;
    private bool _arrived;
    private readonly TimeProvider _clock;

    internal NeedsYouRowViewModel(NeedsYouEntry entry, bool seen, bool reducedMotion, bool arrived, TimeProvider clock)
    {
        Entry = entry;
        _clock = clock;
        (_seen, _reduced, _arrived) = (seen, reducedMotion, arrived);
        Controls = BuildControls();
        ChildAnswer = ChildAnswerOf(entry);
    }

    /// <summary>The "Answer in the child" control of a row whose answer a child decides (the row's served via); null for any other row.</summary>
    public ChildAnswerViewModel? ChildAnswer { get; private set; }

    private static ChildAnswerViewModel? ChildAnswerOf(NeedsYouEntry entry) =>
        InstanceRules.IsChildVia(entry.Via) ? ChildAnswerViewModel.ForNeedsYouRow(entry.Id, entry.ShownId.Length > 0 ? entry.ShownId : entry.Id, entry.Child) : null;

    /// <summary>The row's chip as served, the one chip control draws it; null for none.</summary>
    public StateChip? Chip => Entry.Chip;

    /// <summary>The id shown at rest; empty when the row shows none.</summary>
    public string ShownId => Entry.ShownId;

    /// <summary>The row's title.</summary>
    public string Title => Entry.Title;

    /// <summary>The repo tag; empty for none.</summary>
    public string Repo => Entry.Repo;

    /// <summary>The served label, the secondary line.</summary>
    public string Label => Entry.Label;

    /// <summary>The served reason sentence (the sheet shows it).</summary>
    public string Reason => Entry.Reason;

    /// <summary>True when the sheet shows the reason as its own line: a row with no details of its own (a park, a decision) has nowhere else for it.</summary>
    public bool HasReasonLine => Entry.Reason.Length > 0 && Details.Count == 0;

    /// <summary>The one served command to copy; empty for none.</summary>
    public string Cli => Entry.Cli;

    /// <summary>How long the row has waited, as of the last update ("12m", "2h").</summary>
    public string Age => Entry.Since == default ? "" : Labels.RowAge(_clock.GetUtcNow() - Entry.Since);

    /// <summary>The name screen readers hear: the urgency word, the chip, the title and the id, and "new" while the row is unseen (§11.3).</summary>
    public string AccessibleName => Labels.NeedsYouRowName(UrgencyWord, Entry.Chip?.Word ?? "", Entry.Title, IdShown, Entry.Urgency == NeedsYouUrgency.Pulse && !_seen);

    private string UrgencyWord => Entry.Urgency switch
    {
        NeedsYouUrgency.Pulse => Labels.UrgencyPulse,
        NeedsYouUrgency.Colour => Labels.UrgencyColour,
        NeedsYouUrgency.Calm => Labels.UrgencyCalm,
        _ => throw new InvalidOperationException("an urgency the app doesn't know"),
    };

    private string IdShown => Entry.ShownId.Length > 0 ? Entry.ShownId : Entry.Id;

    /// <summary>The title button's accessible name: "Open", the title and the id.</summary>
    public string OpenName => Labels.OpenRow(Title, IdShown);

    /// <summary>The row's controls, the primary first, then the others as served.</summary>
    public IReadOnlyList<NeedsYouActionControl> Controls { get; private set; }

    /// <summary>Where Enter and a click on the title go: the row's own sheet, keyed on the row id (never the subject).</summary>
    public DeepLink SheetLink => new(DeepLinkKind.NeedsYou, Entry.Id);

    /// <summary>True when the row names an item, so it offers Open.</summary>
    public bool CanOpenItem => Entry.ItemId is not null;

    /// <summary>Where Open goes; null when the row names no item.</summary>
    public DeepLink? ItemLink => Entry.ItemId is { } item ? new DeepLink(DeepLinkKind.Item, item) : null;

    /// <summary>The served recorded-only sentence; empty when the row isn't recorded-only.</summary>
    public string Notice => Entry.RecordedOnly ? Entry.Notice : "";

    /// <summary>True when the row shows <see cref="Notice"/>.</summary>
    public bool HasNotice => Notice.Length > 0;

    /// <summary>True while the views are stalled: the row stays, marked stale.</summary>
    public bool IsStale { get; private set; }

    /// <summary>True for the row the keys act on.</summary>
    public bool IsSelected { get; private set; }

    /// <summary>The served row, as of the last update.</summary>
    internal NeedsYouEntry Entry { get; private set; }

    /// <summary>Takes the row as the API serves it now; everything read from it is raised as changed.</summary>
    /// <param name="entry">The row.</param>
    internal void Replace(NeedsYouEntry entry)
    {
        if (!entry.Equals(Entry))
        {
            var before = (Tint, HasOutline, SlidesIn, HasNewWord);
            Entry = entry;
            ChildAnswer = ChildAnswerOf(entry);
            foreach (var name in _content)
            {
                OnChanged(name);
            }

            RebuildControls();
            ReportLook(before);
        }
        else
        {
            OnChanged(nameof(Age));
        }
    }

    /// <summary>Sets what the list knows of the row: stale views, a rebuild in progress, and whether the keys act on it.</summary>
    /// <param name="stale">The views are stalled.</param>
    /// <param name="rebuilding">The views are rebuilding.</param>
    /// <param name="selected">The keys act on this row.</param>
    internal void SetContext(bool stale, bool rebuilding, bool selected)
    {
        if (stale != IsStale)
        {
            IsStale = stale;
            OnChanged(nameof(IsStale));
        }

        if (rebuilding != _rebuilding)
        {
            _rebuilding = rebuilding;
            RebuildControls();
        }

        if (selected != IsSelected)
        {
            IsSelected = selected;
            OnChanged(nameof(IsSelected));
            OnChanged(nameof(InboxName));
        }
    }

    private static readonly string[] _content =
    [
        nameof(Chip), nameof(Details), nameof(PrimaryDetails), nameof(MoreDetails), nameof(HasMoreDetails), nameof(ShowsCliUpFront), nameof(ShownId), nameof(Title), nameof(Repo), nameof(Label), nameof(Reason), nameof(HasReasonLine), nameof(Cli), nameof(Age), nameof(AccessibleName),
        nameof(CanOpenItem), nameof(ItemLink), nameof(ChildAnswer), nameof(Notice), nameof(HasNotice), nameof(InboxLine), nameof(InboxName),
    ];

    private bool _rebuilding;

    // The buttons are kept when the served actions are the same ones (a re-read serves equal rows again): a button with
    // focus is not rebuilt under the user (§3.3).
    private void RebuildControls()
    {
        var built = BuildControls();
        if (!built.SequenceEqual(Controls))
        {
            Controls = built;
            OnChanged(nameof(Controls));
        }
    }

    // A decision's options keep their served order; only the recommended one is primary. Any other action is primary when it is the
    // row's primary action, and goes first.
    private NeedsYouActionControl[] BuildControls() =>
    [
        .. Entry.Actions
            .Select(a => new NeedsYouActionControl(a, IsPrimary(a), a.IsReady && !_rebuilding, Tooltip(a, _rebuilding), IdShown))
            .OrderByDescending(c => c.IsPrimary && c.Action.OptionId.Length == 0),
    ];

    private bool IsPrimary(NeedsYouAction action) =>
        action.OptionId.Length > 0 ? action.Recommended : string.Equals(action.CatalogId, Entry.PrimaryAction, StringComparison.Ordinal);

    private static string Tooltip(NeedsYouAction action, bool rebuilding) => action.Readiness switch
    {
        ActionReadiness.Ready when rebuilding => Labels.RetryAfterRebuild,
        ActionReadiness.CliOnly => Labels.WithHint(Labels.ProtectedTooltip, action.FillInHint),
        ActionReadiness.Pending => Labels.WithHint(action.Cli, action.FillInHint),
        _ => "",
    };

    /// <summary>The labelled lines of the row's sheet beyond its title and reason: a failure's service and actions, a profile's changes.</summary>
    public IReadOnlyList<SheetDetail> Details
    {
        get
        {
            var key = (Entry, Age);
            if (_detailsFor != key)
            {
                field = Entry.Subject?.Kind switch
                {
                    NeedsYouKinds.Service => FailureDetails.Build(Entry, Age),
                    NeedsYouKinds.Profile => ProfileDetails.Build(Entry),
                    _ => [],
                };
                _detailsFor = key;
            }

            return field;
        }
    } = [];

    // Built when the row or its age changes, not on every read.
    private (NeedsYouEntry? Entry, string Age) _detailsFor;

    /// <summary>The row's id.</summary>
    public string Id => Entry.Id;

    /// <summary>The row's tint.</summary>
    public RowTint Tint => Entry.Urgency switch
    {
        NeedsYouUrgency.Pulse => _seen ? RowTint.None : _reduced ? RowTint.Steady : RowTint.Pulsing,
        NeedsYouUrgency.Colour => RowTint.Steady,
        NeedsYouUrgency.Calm => RowTint.Subtle,
        _ => throw new InvalidOperationException("an urgency the app doesn't know"),
    };

    /// <summary>True for a steady tint (a failure; an unseen pulse row under reduced motion).</summary>
    public bool IsSteady => Tint == RowTint.Steady;

    /// <summary>True for the subtle tint of a calm row.</summary>
    public bool IsSubtle => Tint == RowTint.Subtle;

    /// <summary>True when the tint pulses (an unseen pulse row, motion allowed).</summary>
    public bool IsPulsing => Tint == RowTint.Pulsing;

    /// <summary>True for the static 2 px attention outline (an unseen pulse row under reduced motion).</summary>
    public bool HasOutline => Entry.Urgency == NeedsYouUrgency.Pulse && !_seen && _reduced;

    /// <summary>True for an unseen pulse row, with motion or without: motion only adds to the word "new" (UI spec §11.4).</summary>
    public bool HasNewWord => Entry.Urgency == NeedsYouUrgency.Pulse && !_seen;

    /// <summary>The word "new" next to an unseen pulse row; empty otherwise.</summary>
    public string NewWord => HasNewWord ? Labels.New : "";

    /// <summary>True when the row slides in (it arrived after the list was first shown, and motion is allowed).</summary>
    public bool SlidesIn => _arrived && !_reduced;

    /// <summary>Applies the panel's current state to the row, raising a change for each property that differs.</summary>
    /// <param name="seen">Whether the row has been seen.</param>
    /// <param name="reducedMotion">Whether motion is reduced.</param>
    /// <param name="arrived">Whether the row arrived in the latest update.</param>
    internal void Apply(bool seen, bool reducedMotion, bool arrived)
    {
        var before = (Tint, HasOutline, SlidesIn, HasNewWord);
        (_seen, _reduced, _arrived) = (seen, reducedMotion, arrived);
        ReportLook(before);
    }

    // Reports the look properties (tint, outline, slide-in, the word new) that differ from how they were.
    private void ReportLook((RowTint Tint, bool HasOutline, bool SlidesIn, bool HasNewWord) before)
    {
        if (before.Tint != Tint)
        {
            OnChanged(nameof(AccessibleName));
            OnChanged(nameof(InboxName));
            OnChanged(nameof(Tint));
            OnChanged(nameof(IsPulsing));
            OnChanged(nameof(IsSteady));
            OnChanged(nameof(IsSubtle));
        }

        if (before.HasOutline != HasOutline)
        {
            OnChanged(nameof(HasOutline));
        }

        if (before.SlidesIn != SlidesIn)
        {
            OnChanged(nameof(SlidesIn));
        }

        if (before.HasNewWord != HasNewWord)
        {
            OnChanged(nameof(HasNewWord));
            OnChanged(nameof(NewWord));
        }
    }
}
