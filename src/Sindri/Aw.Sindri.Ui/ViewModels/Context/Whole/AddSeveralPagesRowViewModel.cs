using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One block of the "Add several pages" list (pages spec §3.6): what it matches, its action lines, why it isn't added, and the tick that says whether Save all writes it.</summary>
internal sealed class AddSeveralPagesRowViewModel : ObservableObject
{
    private readonly PagesPreviewBlock _block;
    private readonly string? _repo;

    /// <summary>Builds the row of a block; a block the server didn't tick starts unticked and stays so.</summary>
    /// <param name="block">The block.</param>
    /// <param name="known">The concepts a picker offers for a concept name that matched none.</param>
    /// <param name="repo">The repo the sheet read the list for, which a page built here is written into; null when none was asked for.</param>
    internal AddSeveralPagesRowViewModel(PagesPreviewBlock block, IReadOnlyList<ConceptChoice>? known = null, string? repo = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        _block = block;
        _repo = repo;
        // The server leaves a block with an unknown concept unticked with its reason and no page: the owner picks a concept for each unknown name and the page is built from the block.
        if (block.Page is null && block.Reason is { } reason && reason.StartsWith(Labels.AddSeveralPagesUnknownConceptStart, StringComparison.Ordinal))
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

    /// <summary>Gets the page's name as cleaned.</summary>
    public string Name => _block.Name;

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
    public string Reason => NeedsConcept ? string.Empty : _block.Reason ?? string.Empty;

    /// <summary>Gets a value indicating whether the row has a reason line; a row with pickers shows their lines instead.</summary>
    public bool HasReason => !NeedsConcept && _block.Reason is not null;

    /// <summary>Gets a value indicating whether the row has something for the owner to settle: a reason, or a concept to pick.</summary>
    public bool HasProblem => HasReason || NeedsConcept;

    /// <summary>Gets a value indicating whether the row can be ticked: the server ticked it and gave no reason, or every unknown concept has a pick.</summary>
    public bool CanTick => NeedsConcept ? Unknowns.All(static u => u.Selected is not null) : _block.Reason is null && _block.Page is not null;

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

    /// <summary>Gets the page Save all sends for this row, or null when the row has none: the server's page, else the block with every unknown concept replaced by its pick.</summary>
    internal PagesPreviewPage? Page => NeedsConcept && CanTick ? Built() : _block.Page;

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
    }

    private PagesPreviewPage Built() =>
        new(_block.Replaces, _block.Name, _repo, _block.Match?.Key, [.. _block.Actions.Select(Built)]);

    private PagesPreviewPageAction Built(PagesPreviewAction action) =>
        new(action.Name, action.Match?.Key, [.. action.Concepts.Select(Concept).OfType<string>().Distinct(StringComparer.Ordinal)]);

    private string? Concept(PagesPreviewConcept concept) =>
        concept.Id ?? Unknowns.FirstOrDefault(u => string.Equals(u.Written, concept.Written, StringComparison.OrdinalIgnoreCase))?.Selected?.Id;
}
