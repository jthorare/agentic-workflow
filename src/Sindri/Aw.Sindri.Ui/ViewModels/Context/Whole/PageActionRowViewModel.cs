namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One action under a page: its name, a link to its node, and the rest of the row in words.</summary>
/// <param name="Key">The action's node key.</param>
/// <param name="Name">The action's name.</param>
/// <param name="Detail">The rest of the row: "· on Schedule week · also in Approval workflow · 2 decisions · code: SubmitButton.tsx".</param>
internal sealed record PageActionRowViewModel(string Key, string Name, string Detail)
{
    /// <summary>Gets the row as a screen reader reads it, one tab stop: its name, then the rest.</summary>
    public string RowName => Name + " " + Detail;
}
