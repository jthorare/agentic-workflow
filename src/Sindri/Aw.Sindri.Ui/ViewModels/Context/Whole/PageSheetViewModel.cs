namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The page sheet (pages spec §3.6): Name this page, Edit page and New page in one right sheet. It fills from <see cref="IPageWrites"/>, edits a name and its action rows with one
/// check box per concept, and writes the page in one profile write; <see cref="PageSheetViewModel"/> (continued) holds the write, the question before Remove page and the question before a discard.
/// </summary>
internal sealed partial class PageSheetViewModel : ObservableObject
{
    private readonly IPageWrites _writes;
    private readonly IReadOnlyList<ConceptChoice> _known;
    private readonly IReadOnlyList<string> _otherNames;
    private readonly string? _openConcept;
    private List<ConceptChoice> _columns = [];
    private PageDraft? _draft;
    private string? _hash;
    private bool _approve;
    private IReadOnlyList<string> _before = [];
    private string _initial = string.Empty;

    /// <summary>Builds a sheet that has read nothing yet.</summary>
    /// <param name="writes">Where the page is read and written.</param>
    /// <param name="known">The concepts the Context page read, in its order, for the names and the "Relate to another concept" picker.</param>
    /// <param name="otherNames">The names of the section's other pages, for the same-name warning.</param>
    /// <param name="openConcept">The concept id the section is open on, or null.</param>
    internal PageSheetViewModel(IPageWrites writes, IReadOnlyList<ConceptChoice> known, IReadOnlyList<string> otherNames, string? openConcept)
    {
        ArgumentNullException.ThrowIfNull(writes);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(otherNames);
        (_writes, _known, _otherNames, _openConcept) = (writes, known, otherNames, openConcept);
        IsLoading = true;
    }

    /// <summary>Raised once the page is written: the section reads again.</summary>
    internal event EventHandler? Saved;

    /// <summary>Raised when the sheet is done and goes away, saved or not.</summary>
    internal event EventHandler? Closed;

    /// <summary>Gets the section row's node key the sheet opened from, or null for a new page.</summary>
    internal string? OpenedKey { get; private set; }

    /// <summary>Gets the id of the page last written, or null.</summary>
    internal string? SavedId { get; private set; }

    /// <summary>Gets the sheet's title: "Name this page", "Edit page" or "New page".</summary>
    public string Title { get; private set; } = Labels.ContextPagesNewPage;

    /// <summary>Gets a value indicating whether the page is the owner's (Edit page: Remove page shows).</summary>
    public bool IsOwnerPage => _draft is { Owner: true };

    /// <summary>Gets a value indicating whether the page is read.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets a value indicating whether the read failed.</summary>
    public bool IsUnreadable { get; private set; }

    /// <summary>Gets a value indicating whether the fields show.</summary>
    public bool IsReady => !IsLoading && !IsUnreadable;

    /// <summary>Gets the line while the page is read.</summary>
    public string LoadingText { get; } = Labels.PageSheetLoading;

    /// <summary>Gets the line when the page can't be read.</summary>
    public string UnreadableText { get; } = Labels.PageSheetUnreadable;

    /// <summary>Gets the close affordance's words.</summary>
    public string EscText { get; } = Labels.PageSheetEsc;

    /// <summary>Gets the close affordance's name for a screen reader.</summary>
    public string CloseName { get; } = Labels.PageSheetCloseName;

    /// <summary>Gets the Name field's label.</summary>
    public string NameLabel { get; } = Labels.PageSheetNameLabel;

    /// <summary>Gets or sets the page's name.</summary>
    public string Name
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(HasSameName));
            OnChanged(nameof(SameNameText));
        }
    } = string.Empty;

    /// <summary>Gets the server's sentence for the Name field, or empty.</summary>
    public string NameError { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the Name field has an error line.</summary>
    public bool HasNameError => NameError.Length > 0;

    /// <summary>Gets a value indicating whether another row of the section has this name (a warning, it doesn't block).</summary>
    public bool HasSameName => Name.Trim().Length > 0 && _otherNames.Any(n => string.Equals(n, Name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Gets "Another page is called Schedule week".</summary>
    public string SameNameText => Labels.PageSheetSameName(Name.Trim());

    /// <summary>Gets the page's map page as a read-only line ("From the map: GET /schedule/week"), or empty.</summary>
    public string FromMap => PageRows.FromMap(_draft?.Surface);

    /// <summary>Gets a value indicating whether the map line shows.</summary>
    public bool HasFromMap => FromMap.Length > 0;

    /// <summary>Gets the actions heading.</summary>
    public string ActionsHeading { get; } = Labels.PageSheetActionsHeading;

    /// <summary>Gets the action rows.</summary>
    public IReadOnlyList<PageSheetActionViewModel> Actions { get; private set; } = [];

    /// <summary>Gets the Add an action button's words.</summary>
    public string AddActionText { get; } = Labels.PageSheetAddAction;

    /// <summary>Gets the picker's label.</summary>
    public string RelateLabel { get; } = Labels.PageSheetRelateAnother;

    /// <summary>Gets the concepts that have no column yet.</summary>
    public IReadOnlyList<ConceptChoice> RelateChoices => [.. _known.Where(k => _columns.TrueForAll(c => !string.Equals(c.Id, k.Id, StringComparison.Ordinal)))];

    /// <summary>Gets a value indicating whether the picker shows: some concept has no column yet.</summary>
    public bool HasRelateChoices => RelateChoices.Count > 0;

    /// <summary>Gets or sets the concept picked in the picker; picking adds an unticked column to every action and clears the picker.</summary>
    public ConceptChoice? SelectedRelate
    {
        get => null;
        set
        {
            if (value is not null)
            {
                AddColumn(value);
            }
        }
    }

    /// <summary>Gets the concept names, in the sheet's column order.</summary>
    internal IReadOnlyList<ConceptChoice> Columns => _columns;

    /// <summary>Reads an existing page (a suggested one or the owner's) and the profile, then fills the sheet.</summary>
    /// <param name="key">The section row's node key.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>When the sheet is filled, or marked unreadable.</returns>
    internal async Task OpenPage(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        OpenedKey = key;
        var draft = await _writes.Page(key, _openConcept, cancellationToken).ConfigureAwait(true);
        if (draft is null)
        {
            (IsLoading, IsUnreadable) = (false, true);
            ChangedState();
            return;
        }

        await ReadProfile(draft.Id is { Length: > 0 } id ? [id] : [], cancellationToken).ConfigureAwait(true);
        Fill(draft);
    }

    /// <summary>Opens an empty page: no map page, one empty action row.</summary>
    /// <param name="cancellationToken">Cancels the read of the profile.</param>
    /// <returns>When the sheet is filled.</returns>
    internal async Task OpenNew(CancellationToken cancellationToken)
    {
        await ReadProfile([], cancellationToken).ConfigureAwait(true);
        Fill(new PageDraft(Id: null, Name: string.Empty, Repo: null, Surface: null, Owner: false, Actions: [new PageActionDraft(Id: null, Name: string.Empty, Action: null, Concepts: [])]));
    }

    /// <summary>Adds an empty action row, with an unticked box in every column.</summary>
    internal void AddAction()
    {
        Actions = [.. Actions, Row(new PageActionDraft(Id: null, Name: string.Empty, Action: null, Concepts: []))];
        Renumber();
    }

    /// <summary>Removes an action row.</summary>
    /// <param name="row">The row.</param>
    internal void RemoveAction(PageSheetActionViewModel row)
    {
        Actions = [.. Actions.Where(a => !ReferenceEquals(a, row))];
        Renumber();
    }

    /// <summary>Gets a value indicating whether anything was typed, ticked, added or removed since the sheet filled.</summary>
    internal bool HasChanges => IsReady && !string.Equals(Snapshot(), _initial, StringComparison.Ordinal);

    // The button's label and the hash come from the profile as it is now: only this page waiting means one click saves and approves.
    // The hash is the file's at open, what the writer compares with (#1826): the live hash can lag a hand edit, and a change after open still clashes.
    private async Task ReadProfile(IReadOnlyList<string> pageIds, CancellationToken cancellationToken)
    {
        var waiting = await _writes.Waiting(cancellationToken).ConfigureAwait(true);
        (_hash, _approve, _before) = (waiting?.FileHash ?? waiting?.LiveHash, waiting?.OnlyPages(pageIds) == true, waiting?.ChangedPaths ?? []);
        if (_hash is null)
        {
            Say(Labels.PageSheetNoHash);
        }
    }

    private void Fill(PageDraft draft)
    {
        _draft = draft;
        Title = draft.Owner ? Labels.ContextPagesEditPage : draft.Surface is null && draft.Id is null ? Labels.ContextPagesNewPage : Labels.ContextPagesNameThis;
        _columns = [];
        foreach (var id in (_openConcept is null ? [] : (string[])[_openConcept]).Concat(draft.Actions.SelectMany(a => a.Concepts)))
        {
            if (_columns.TrueForAll(c => !string.Equals(c.Id, id, StringComparison.Ordinal)))
            {
                _columns.Add(_known.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.Ordinal)) ?? new ConceptChoice(id, id));
            }
        }

        Name = draft.Name;
        Actions = [.. draft.Actions.Select(Row)];
        (IsLoading, IsUnreadable) = (false, false);
        Renumber();
        _initial = Snapshot();
        ChangedState();
    }

    private PageSheetActionViewModel Row(PageActionDraft action) =>
        new(action.Id, action.Name, action.Action, [.. _columns.Select(c => new ConceptTickViewModel(c, action.Concepts.Contains(c.Id, StringComparer.Ordinal)))]);

    private void AddColumn(ConceptChoice concept)
    {
        if (_columns.Exists(c => string.Equals(c.Id, concept.Id, StringComparison.Ordinal)))
        {
            return;
        }

        _columns.Add(concept);
        foreach (var action in Actions)
        {
            action.AddColumn(concept);
        }

        OnChanged(nameof(RelateChoices));
        OnChanged(nameof(HasRelateChoices));
        OnChanged(nameof(SelectedRelate));
    }

    private void Renumber()
    {
        for (var i = 0; i < Actions.Count; i++)
        {
            Actions[i].Place(i + 1, Actions.Count);
        }

        OnChanged(nameof(Actions));
    }

    private string Snapshot() => Name + '\n' + string.Join('\n', Actions.Select(static a => a.Name + "|" + string.Join(',', a.TickedIds)));

    private void ChangedState()
    {
        foreach (var name in (string[])[nameof(Title), nameof(IsOwnerPage), nameof(IsLoading), nameof(IsUnreadable), nameof(IsReady), nameof(FromMap), nameof(HasFromMap), nameof(Actions), nameof(RelateChoices), nameof(HasRelateChoices), nameof(SaveText), nameof(CanSave), nameof(ShowRemovePage)])
        {
            OnChanged(name);
        }
    }
}
