namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One tab over an opened concept's list (#3674): All, Code, Features or Records, with the count it holds.</summary>
/// <param name="Key">all, code, feature or record.</param>
/// <param name="Text">The words on the tab: "Code 1980".</param>
/// <param name="IsSelected">True for the tab the list is of.</param>
/// <param name="CanChoose">False for a tab that holds nothing: it shows 0 and does not answer a click.</param>
internal sealed record ConceptTabItem(string Key, string Text, bool IsSelected, bool CanChoose);
