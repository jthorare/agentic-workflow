namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A concept the page sheet can relate an action to: its id (what is written) and its name (what is read).</summary>
/// <param name="Id">The concept's id.</param>
/// <param name="Name">The concept's name.</param>
internal sealed record ConceptChoice(string Id, string Name);
