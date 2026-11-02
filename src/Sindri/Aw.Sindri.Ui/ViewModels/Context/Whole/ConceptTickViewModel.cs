namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One concept check box under an action in the page sheet.</summary>
/// <param name="concept">The concept it ticks.</param>
/// <param name="ticked">Whether it starts ticked.</param>
internal sealed class ConceptTickViewModel(ConceptChoice concept, bool ticked) : ObservableObject
{
    /// <summary>Gets the concept's id.</summary>
    public string Id { get; } = concept.Id;

    /// <summary>Gets the concept's name, beside the box.</summary>
    public string Name { get; } = concept.Name;

    /// <summary>Gets the box's name for a screen reader.</summary>
    public string AccessibleName { get; } = Labels.PageSheetTick(concept.Name);

    /// <summary>Gets or sets a value indicating whether the action relates to the concept.</summary>
    public bool IsTicked
    {
        get;
        set => Set(ref field, value);
    } = ticked;
}
