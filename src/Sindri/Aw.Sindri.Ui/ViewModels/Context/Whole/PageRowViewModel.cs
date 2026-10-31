namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One page in the Pages and actions section: its name, its marks, the map's page it names, and its actions.</summary>
/// <param name="Key">The page's node key.</param>
/// <param name="Name">The page's name.</param>
/// <param name="Marks">Its marks, in words (pages spec §3.5).</param>
/// <param name="FromMap">"From the map: GET /schedule/week", or empty for a page the owner added with no map page.</param>
/// <param name="Actions">Its actions.</param>
internal sealed record PageRowViewModel(string Key, string Name, IReadOnlyList<string> Marks, string FromMap, IReadOnlyList<PageActionRowViewModel> Actions)
{
    /// <summary>Gets a value indicating whether the owner named the page (its button is Edit page, not Name this page).</summary>
    public bool Owner { get; init; }

    /// <summary>Gets a value indicating whether the host wired the sheet, so the row's button shows.</summary>
    public bool CanWrite { get; init; }

    /// <summary>Gets the row's button: "Edit page" on an owner page, "Name this page" on a suggested one.</summary>
    public string ButtonText => Owner ? Labels.ContextPagesEditPage : Labels.ContextPagesNameThis;

    /// <summary>Gets the button's name for a screen reader: the words and the page.</summary>
    public string ButtonName => ButtonText + ": " + Name;

    /// <summary>Gets a value indicating whether the From the map line shows.</summary>
    public bool HasFromMap => FromMap.Length > 0;

    /// <summary>Gets the row as a screen reader reads it: its name, then its marks.</summary>
    public string RowName => Labels.ContextPagesRowName(Name, Marks);
}
