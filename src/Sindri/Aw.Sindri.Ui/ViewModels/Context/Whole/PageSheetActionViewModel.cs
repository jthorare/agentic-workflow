namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One action row of the page sheet: its name box, its concept check boxes and what the map says about it.</summary>
internal sealed class PageSheetActionViewModel : ObservableObject
{
    private int _number = 1;
    private int _total = 1;

    /// <summary>Builds a row.</summary>
    /// <param name="id">The action id, or null for a new action.</param>
    /// <param name="name">The name.</param>
    /// <param name="action">The map action's node key, or null.</param>
    /// <param name="ticks">One check box per concept column, in the sheet's column order.</param>
    internal PageSheetActionViewModel(string? id, string name, string? action, IReadOnlyList<ConceptTickViewModel> ticks)
    {
        Id = id;
        Action = action;
        Name = name;
        Ticks = ticks;
        foreach (var tick in ticks)
        {
            tick.PropertyChanged += (_, _) => Touched();
        }
    }

    /// <summary>Gets the action id, or null for a new action (the server makes it from the name).</summary>
    internal string? Id { get; }

    /// <summary>Gets the map action's node key, or null.</summary>
    internal string? Action { get; }

    /// <summary>Gets or sets the name.</summary>
    public string Name
    {
        get;
        set
        {
            Set(ref field, value);
            Touched();
        }
    }

    /// <summary>Gets the check boxes, in the sheet's column order.</summary>
    public IReadOnlyList<ConceptTickViewModel> Ticks { get; private set; }

    /// <summary>Gets the name box's label.</summary>
    public string NameLabel { get; } = Labels.PageSheetActionNameLabel;

    /// <summary>Gets the Remove button's words.</summary>
    public string RemoveText { get; } = Labels.PageSheetActionRemove;

    /// <summary>Gets "From the map: Submit button", or "No code found yet" for an action the map doesn't know.</summary>
    public string FromMap => PageRows.FromMap(Action) is { Length: > 0 } line ? line : Labels.ContextPagesNoCode;

    /// <summary>Gets the server's sentence for this row, or empty.</summary>
    public string Error
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    /// <summary>Gets a value indicating whether the row has an error line.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Gets the row as a screen reader reads it: "Action 2 of 3, Submit a schedule, relates to 2 concepts".</summary>
    public string RowName => Labels.PageSheetActionRow(_number, _total, Name.Trim(), Ticks.Count(static t => t.IsTicked));

    /// <summary>Gets the ids of the concepts the row relates to, in column order.</summary>
    internal IReadOnlyList<string> TickedIds => [.. Ticks.Where(static t => t.IsTicked).Select(static t => t.Id)];

    /// <summary>Sets what the row reads out about its place in the list.</summary>
    /// <param name="number">The row's number, from 1.</param>
    /// <param name="total">How many rows there are.</param>
    internal void Place(int number, int total)
    {
        (_number, _total) = (number, total);
        OnChanged(nameof(RowName));
    }

    /// <summary>Shows or clears the server's sentence for this row.</summary>
    /// <param name="message">The sentence, or empty to clear.</param>
    internal void Refuse(string message)
    {
        Error = message;
        OnChanged(nameof(HasError));
    }

    /// <summary>Appends a column: one more unticked check box.</summary>
    /// <param name="concept">The concept to add.</param>
    internal void AddColumn(ConceptChoice concept)
    {
        var tick = new ConceptTickViewModel(concept, ticked: false);
        tick.PropertyChanged += (_, _) => Touched();
        Ticks = [.. Ticks, tick];
        OnChanged(nameof(Ticks));
        Touched();
    }

    private void Touched() => OnChanged(nameof(RowName));
}
