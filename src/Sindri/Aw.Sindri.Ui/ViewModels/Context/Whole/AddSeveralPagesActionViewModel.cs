namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One action of a row in Accept all suggested: its name and one concept check box per concept the sheet knows, pre-ticked from the page's draft (the page sheet's per-action shape).</summary>
internal sealed class AddSeveralPagesActionViewModel : ObservableObject
{
    /// <summary>Builds the action of a draft.</summary>
    /// <param name="draft">The action as the map holds it.</param>
    /// <param name="columns">The concepts a box is offered for, in the sheet's order.</param>
    internal AddSeveralPagesActionViewModel(PageActionDraft draft, IReadOnlyList<ConceptChoice> columns)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(columns);
        Key = draft.Action ?? draft.Id ?? draft.Name;
        Name = draft.Name;
        Ticks = [.. columns.Select(c => new ConceptTickViewModel(c, draft.Concepts.Contains(c.Id, StringComparer.Ordinal)))];
        foreach (var tick in Ticks)
        {
            tick.PropertyChanged += (_, _) => OnChanged(nameof(Ticks));
        }
    }

    /// <summary>Gets what a re-read matches the action by: its map key, else its id, else its name.</summary>
    internal string Key { get; }

    /// <summary>Gets the action's name.</summary>
    public string Name { get; }

    /// <summary>Gets the concept check boxes, in the sheet's order.</summary>
    public IReadOnlyList<ConceptTickViewModel> Ticks { get; }

    /// <summary>Gets the ids of the concepts the action relates to, in the sheet's order.</summary>
    internal IReadOnlyList<string> TickedIds => [.. Ticks.Where(static t => t.IsTicked).Select(static t => t.Id)];

    /// <summary>Ticks exactly these concepts.</summary>
    /// <param name="ids">The concept ids.</param>
    internal void Retick(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var tick in Ticks)
        {
            tick.IsTicked = ids.Contains(tick.Id, StringComparer.Ordinal);
        }
    }
}
