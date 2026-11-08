namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>One field of a conflict column: its label, that version's value and the "changed" mark when the two versions differ on it (todo spec §3.1).</summary>
/// <param name="label">"Text", "Due", "Links" or "State".</param>
/// <param name="value">That version's value.</param>
internal sealed class AttentionFieldViewModel(string label, string value)
{
    /// <summary>"Text", "Due", "Links" or "State".</summary>
    public string Label { get; } = label;

    /// <summary>That version's value.</summary>
    public string Value { get; } = value;

    /// <summary>The mark beside the label: "changed".</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Mark => Labels.TodoAttnChanged;
#pragma warning restore CA1822, MA0041
}
