using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One block of the "Add several pages" list (pages spec §3.6): what it matches, its action lines, why it isn't added, and the tick that says whether Save all writes it.</summary>
internal sealed class AddSeveralPagesRowViewModel : ObservableObject
{
    private readonly PagesPreviewBlock _block;

    /// <summary>Builds the row of a block; a block the server didn't tick starts unticked and stays so.</summary>
    /// <param name="block">The block.</param>
    /// <param name="known">The concepts a picker offers for a concept name that matched none.</param>
    internal AddSeveralPagesRowViewModel(PagesPreviewBlock block, IReadOnlyList<ConceptChoice>? known = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        _block = block;
        Name = block.Name;
        // The server sends the page of a block with an unknown concept, unticked with its reason: the owner picks a concept for each unknown name and Save all swaps the picks into that page.
        if (block.UnknownConcepts.Count > 0 && block.Page is not null)
        {
            Unknowns = [.. block.UnknownConcepts.Select(name => new UnknownConceptViewModel(name, known ?? []))];
            foreach (var unknown in Unknowns)
            {
                unknown.PropertyChanged += (_, _) => Picked();
            }
        }

        Ticked = block.Ticked && block.Reason is null && block.Page is not null;
    }

    /// <summary>Gets one picker per written concept name that matched no concept (pages spec §3.6); empty for an ordinary row.</summary>
    public IReadOnlyList<UnknownConceptViewModel> Unknowns { get; } = [];

    /// <summary>Gets a value indicating whether the row shows pickers.</summary>
    public bool NeedsConcept => Unknowns.Count > 0;

    /// <summary>Gets the block's number, from 1.</summary>
    public int Block => _block.Block;

    /// <summary>Builds the row of a page the map already holds, as Accept all suggested lists it: no match, no paste block, ticked unless a reason says otherwise.</summary>
    /// <param name="draft">The page.</param>
    /// <param name="number">Its place in the list, from 1.</param>
    /// <param name="reason">Why the row is not added, or null.</param>
    /// <param name="known">The concepts a picker offers.</param>
    /// <param name="pickConcept">True for a page in no concept: the row gets a concept picker, and a page with no actions is not added.</param>
    /// <returns>The row.</returns>
    internal static AddSeveralPagesRowViewModel FromDraft(PageDraft draft, int number, string? reason, IReadOnlyList<ConceptChoice>? known = null, bool pickConcept = false)
    {
        ArgumentNullException.ThrowIfNull(draft);
        reason = pickConcept && draft.Actions.Count == 0 ? Labels.AcceptAllNoActions : reason;   // nothing to link a concept to
        var page = new PagesPreviewPage(draft.Id, draft.Name, draft.Repo, draft.Surface, [.. draft.Actions.Select(static a => new PagesPreviewPageAction(a.Id, a.Name, a.Action, a.Concepts))]);
        var block = new PagesPreviewBlock(number, number, draft.Name, Match: null, Replaces: null, SameAs: null, Ticked: reason is null, reason, [.. draft.Actions.Select(static a => new PagesPreviewAction(a.Name, Match: null, []))], [], page);
        var columns = Columns(known, draft);
        var row = new AddSeveralPagesRowViewModel(block, known) { Actions = [.. draft.Actions.Select(a => new AddSeveralPagesActionViewModel(a, columns))] };
        foreach (var action in row.Actions)
        {
            action.PropertyChanged += (_, _) => row.OnChanged(nameof(Page));
        }

        if (pickConcept && draft.Actions.Count > 0)
        {
            row.AddPicker(draft.Name, known ?? []);
        }

        return row;
    }

    // The picker offers None and the known concepts, and starts on the one the page's name points at, if any.
    private void AddPicker(string pageName, IReadOnlyList<ConceptChoice> known)
    {
        Picker = new UnknownConceptViewModel(pageName, [new ConceptChoice(string.Empty, Labels.AcceptAllNone), .. known]);
        Picker.PropertyChanged += (_, _) => Picked();
        Picker.Selected = ConceptMatch.Best(pageName, known);
        Ticked = CanTick;   // an empty picker leaves the row unticked
        OnChanged(nameof(HasPicker));
    }

    // The concepts the sheet knows, then any the page's actions relate to that it doesn't (shown under their id).
    private static List<ConceptChoice> Columns(IReadOnlyList<ConceptChoice>? known, PageDraft draft)
    {
        List<ConceptChoice> columns = [.. known ?? []];
        foreach (var id in draft.Actions.SelectMany(static a => a.Concepts).Distinct(StringComparer.Ordinal).Where(id => !columns.Exists(c => string.Equals(c.Id, id, StringComparison.Ordinal))))
        {
            columns.Add(new ConceptChoice(id, id));
        }

        return columns;
    }

    /// <summary>Gets or sets a value indicating whether the row is open into its fields; a row with no fields (a pasted block) stays shut.</summary>
    public bool IsOpen
    {
        get;
        set => Set(ref field, value && HasFields);
    }

    /// <summary>Gets the row's actions with their concept boxes; empty for a pasted block.</summary>
    public IReadOnlyList<AddSeveralPagesActionViewModel> Actions { get; private set; } = [];

    /// <summary>Gets the toggle's words.</summary>
    public string OpenText { get; } = Labels.AcceptAllEdit;

    /// <summary>Gets the toggle's name for a screen reader.</summary>
    public string OpenName => Labels.AcceptAllEditName(Name);

    /// <summary>Gets the name box's label.</summary>
    public string NameLabel { get; } = Labels.AcceptAllNameLabel;

    /// <summary>Gets a value indicating whether the row can open into fields.</summary>
    public bool HasFields => Actions.Count > 0;

    /// <summary>Gets the name and every action's ticks as one string, so a change shows up as a change.</summary>
    internal string Signature => Name + "|" + string.Join(';', Actions.Select(static a => string.Join(',', a.TickedIds)));

    /// <summary>Gets the ticked concept ids by action key, as a refreshed list's row gets them back.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> ActionTicks
    {
        get
        {
            var ticks = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var action in Actions)
            {
                _ = ticks.TryAdd(action.Key, action.TickedIds);
            }

            return ticks;
        }
    }

    /// <summary>Ticks each action's concepts as they were, matched by action key.</summary>
    /// <param name="ticks">The ticked ids by action key.</param>
    internal void RestoreTicks(IReadOnlyDictionary<string, IReadOnlyList<string>> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        foreach (var action in Actions.Where(a => ticks.ContainsKey(a.Key)))
        {
            action.Retick(ticks[action.Key]);
        }
    }

    /// <summary>Gets the concept picker of a page in no concept; null on every other row.</summary>
    public UnknownConceptViewModel? Picker { get; private set; }

    /// <summary>Gets a value indicating whether the row shows a concept picker.</summary>
    public bool HasPicker => Picker is not null;

    /// <summary>Gets the concept id the picker holds, or null when it is empty, says None or the row has none.</summary>
    internal string? PickedId => Picker?.Selected is { Id.Length: > 0 } picked ? picked.Id : null;

    private bool NoneIsPicked => Picker?.Selected is { Id.Length: 0 };

    /// <summary>Picks a concept again, as a refreshed list's row gets back the pick of the row it replaces.</summary>
    /// <param name="id">The concept id.</param>
    internal void RestorePick(string id) => Picker?.Selected = Picker.Choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));

    /// <summary>Gets the page's surface key (its name when it has none): what a re-read matches the row by, as block numbers are not stable.</summary>
    internal string Key => _block.Page?.Surface ?? _block.Name;

    /// <summary>Gets or sets the page's name: as cleaned, or as the owner renamed it.</summary>
    public string Name
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>Gets "Matches Schedule week (4 actions found)" or "New page".</summary>
    public string MatchText => _block.Match is { } match ? Labels.AddSeveralPagesMatches(match.Name, match.Actions) : Labels.AddSeveralPagesNewPage;

    /// <summary>Gets the replaces flag's words, or empty.</summary>
    public string ReplacesText => _block.Replaces is null ? string.Empty : Labels.AddSeveralPagesReplaces;

    /// <summary>Gets a value indicating whether the replaces flag shows.</summary>
    public bool HasReplaces => _block.Replaces is not null;

    /// <summary>Gets a value indicating whether the same-page line shows.</summary>
    public bool HasSameAs => _block.SameAs is not null;

    /// <summary>Gets "Same page as block 1", or empty.</summary>
    public string SameAsText => _block.SameAs is { } block ? Labels.AddSeveralPagesSameAs(block) : string.Empty;

    /// <summary>Gets why the row is not added, in the server's sentence, or empty.</summary>
    public string Reason => NeedsConcept ? string.Empty : _block.Reason ?? (NoneIsPicked ? Labels.AcceptAllNoPick : string.Empty);

    /// <summary>Gets a value indicating whether the row has a reason line; a row with pickers shows their lines instead.</summary>
    public bool HasReason => !NeedsConcept && (_block.Reason is not null || NoneIsPicked);

    /// <summary>Gets a value indicating whether the row has something for the owner to settle: a reason, or a concept to pick.</summary>
    public bool HasProblem => HasReason || NeedsConcept;

    /// <summary>Gets a value indicating whether the row can be ticked: the server ticked it and gave no reason, or every unknown concept has a pick.</summary>
    public bool CanTick => NeedsConcept ? Unknowns.All(static u => u.Selected is not null) : _block.Reason is null && _block.Page is not null && (Picker is null || PickedId is not null);

    /// <summary>Gets the tick's name for a screen reader.</summary>
    public string TickName => Labels.AddSeveralPagesTick(Name);

    /// <summary>Gets or sets whether Save all writes this row; a row that can't be ticked stays off.</summary>
    public bool Ticked
    {
        get;
        set => Set(ref field, value && CanTick);
    }

    /// <summary>Gets the action lines: each name, with "Matches ..." after it when it names an action of the matched page.</summary>
    public IReadOnlyList<string> ActionLines => [.. _block.Actions.Select(static a => a.Match is { } m ? a.Name + " - " + Labels.AddSeveralPagesActionMatches(m.Name) : a.Name)];

    /// <summary>Gets the page Save all sends for this row, or null when the row has none: the server's page, with each unknown concept name in its actions replaced by its pick.</summary>
    internal PagesPreviewPage? Page => (NeedsConcept && CanTick && _block.Page is { } page ? WithPicks(page) : _block.Page) is { } sent ? sent with { Name = Name, Actions = WithPick(WithTicks(sent.Actions)) } : null;

    /// <summary>Gets the picks made, by the name written.</summary>
    internal IReadOnlyDictionary<string, string> Picks =>
        Unknowns.Select(static u => (u.Written, u.Selected?.Id)).Where(static p => p.Id is not null).ToDictionary(static p => p.Written, static p => p.Id ?? string.Empty, StringComparer.Ordinal);

    /// <summary>Picks a concept for each unknown name, as a refreshed list's row gets back the picks of the row it replaces.</summary>
    /// <param name="picks">The picked concept ids by the name written.</param>
    internal void Restore(IReadOnlyDictionary<string, string> picks)
    {
        foreach (var unknown in Unknowns)
        {
            unknown.Selected = picks.TryGetValue(unknown.Written, out var id) ? unknown.Choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal)) : null;
        }
    }

    private void Picked()
    {
        Ticked = CanTick;   // a row whose every concept is picked is ticked, as Accept all's filled picker is (pages spec §3.6)
        OnChanged(nameof(CanTick));
        OnChanged(nameof(Reason));
        OnChanged(nameof(HasReason));
        OnChanged(nameof(Page));
    }

    // An opened row's boxes are the actions' concepts; the actions keep the order they were listed in.
    private IReadOnlyList<PagesPreviewPageAction> WithTicks(IReadOnlyList<PagesPreviewPageAction> sent) =>
        Actions.Count != sent.Count ? sent : [.. sent.Select((a, i) => a with { Concepts = Actions[i].TickedIds })];

    // The concept picked for a page in no concept goes onto every action (a page has no concepts of its own).
    private IReadOnlyList<PagesPreviewPageAction> WithPick(IReadOnlyList<PagesPreviewPageAction> actions) =>
        PickedId is { } id ? [.. actions.Select(a => a with { Concepts = [.. a.Concepts.Append(id).Distinct(StringComparer.Ordinal)] })] : actions;

    private PagesPreviewPage WithPicks(PagesPreviewPage page) =>
        page with { Actions = [.. page.Actions.Select(a => a with { Concepts = [.. a.Concepts.Select(Concept).Distinct(StringComparer.Ordinal)] })] };

    private string Concept(string written) =>
        Unknowns.FirstOrDefault(u => string.Equals(u.Written, written, StringComparison.OrdinalIgnoreCase))?.Selected?.Id ?? written;
}
