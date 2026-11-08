namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>What the quick-add box reads from what was typed.</summary>
/// <param name="Text">The text to save, without the day word that became the due day.</param>
/// <param name="Due">The due day, or null for none.</param>
internal readonly record struct QuickParsed(string Text, DateOnly? Due);
