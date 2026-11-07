namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One field of a 409 on the issue sheet: Linear's value beside the owner's, and the button that takes Linear's.</summary>
/// <param name="label">The field's label.</param>
/// <param name="text">"Linear: X · yours: Y".</param>
/// <param name="useLinears">Takes Linear's value for the field.</param>
internal sealed class LinearConflictRowViewModel(string label, string text, Action<LinearConflictRowViewModel> useLinears)
{
    /// <summary>The field's label.</summary>
    public string Label { get; } = label;

    /// <summary>"Linear: X · yours: Y".</summary>
    public string Text { get; } = text;

    /// <summary>The row's accessible name.</summary>
    public string Name => Label + ": " + Text;

    /// <summary>The per-field button's text.</summary>
    public string UseLinearsText { get; } = Labels.LinearUseLinears;

    /// <summary>The per-field button's accessible name.</summary>
    public string UseLinearsName => Labels.LinearUseLinears + " for " + Label;

    /// <summary>Takes Linear's value for this field and drops the row.</summary>
    internal void UseLinears() => useLinears(this);
}
