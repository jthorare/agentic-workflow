using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One block of the "Add several pages" list (pages spec §3.6): what it matches, its action lines, why it isn't added, and the tick that says whether Save all writes it.</summary>
internal sealed class AddSeveralPagesRowViewModel : ObservableObject
{
    private readonly PagesPreviewBlock _block;
    private readonly IReadOnlyList<ConceptChoice> _known;

    /// <summary>Builds the row of a block; a block the server didn't tick starts unticked and stays so.</summary>
    /// <param name="block">The block.</param>
    /// <param name="known">The concepts a picker offers for a concept name that matched none.</param>
    internal AddSeveralPagesRowViewModel(PagesPreviewBlock block, IReadOnlyList<ConceptChoice>? known = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        (_block, _known) = (block, known ?? []);
        Ticked = block.Ticked && block.Reason is null && block.Page is not null;
    }

    /// <summary>Gets one picker per written concept name that matched no concept (pages spec §3.6); empty for an ordinary row.</summary>
    public IReadOnlyList<UnknownConceptViewModel> Unknowns => _known.Count > 1000 ? [] : throw new NotSupportedException();

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
    public string Reason => _block.Reason ?? string.Empty;

    /// <summary>Gets a value indicating whether the row has a reason.</summary>
    public bool HasReason => _block.Reason is not null;

    /// <summary>Gets a value indicating whether the row can be ticked: the server ticked it and gave no reason.</summary>
    public bool CanTick => _block.Reason is null && _block.Page is not null;

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

    /// <summary>Gets the page Save all sends for this row, or null when the row has none.</summary>
    internal PagesPreviewPage? Page => _block.Page;
}
