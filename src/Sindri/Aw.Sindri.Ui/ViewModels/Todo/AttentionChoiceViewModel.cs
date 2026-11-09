namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>One column of an open attention row (todo spec §3.1): a conflict's Sindri or Obsidian version with its fields that differ, and the button under it with its key.</summary>
/// <param name="key">"1" or "2": 1 is Sindri's side everywhere, 2 is Obsidian's.</param>
/// <param name="heading">"Sindri" or "Obsidian"; empty on a hold's buttons.</param>
/// <param name="buttonText">The button's words.</param>
/// <param name="buttonName">The button's name for a screen reader.</param>
/// <param name="fields">The fields where the two versions differ.</param>
/// <param name="isRemoved">True when this version removed the todo.</param>
internal sealed class AttentionChoiceViewModel(string key, string heading, string buttonText, string buttonName, IReadOnlyList<AttentionFieldViewModel> fields, bool isRemoved)
{
    /// <summary>"1" or "2".</summary>
    public string Key { get; } = key;

    /// <summary>"Sindri" or "Obsidian"; empty on a hold's buttons.</summary>
    public string Heading { get; } = heading;

    /// <summary>The button's words ("Keep Sindri's").</summary>
    public string ButtonText { get; } = buttonText;

    /// <summary>The button's name for a screen reader.</summary>
    public string ButtonName { get; } = buttonName;

    /// <summary>The fields where the two versions differ, each with its "changed" mark.</summary>
    public IReadOnlyList<AttentionFieldViewModel> Fields { get; } = fields;

    /// <summary>True when this version removed the todo (the column says "Removed").</summary>
    public bool IsRemoved { get; } = isRemoved;

    /// <summary>True when the column has fields to draw (a hold's buttons have none).</summary>
    public bool HasFields => Fields.Count > 0;
}
