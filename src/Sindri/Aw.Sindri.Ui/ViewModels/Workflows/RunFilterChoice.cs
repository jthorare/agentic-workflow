namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One choice of a runs filter: the key the view model filters by and the words the drop-down shows.</summary>
/// <param name="Key">The key ("failed", "7d"); empty for no filter.</param>
/// <param name="Label">The words.</param>
internal sealed record RunFilterChoice(string Key, string Label);
