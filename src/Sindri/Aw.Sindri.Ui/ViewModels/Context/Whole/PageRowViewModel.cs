namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One page in the Pages and actions section: its name, its marks and its actions.</summary>
/// <param name="Key">The page's node key.</param>
/// <param name="Name">The page's name.</param>
/// <param name="Marks">Its marks, in words (pages spec §3.5).</param>
/// <param name="Actions">Its actions.</param>
internal sealed record PageRowViewModel(string Key, string Name, IReadOnlyList<string> Marks, IReadOnlyList<PageActionRowViewModel> Actions);
