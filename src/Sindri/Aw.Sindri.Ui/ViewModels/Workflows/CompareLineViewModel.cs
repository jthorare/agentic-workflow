namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One line of a step's output in a compare cell, and whether the other run lacks it.</summary>
/// <param name="Text">The line as printed, indentation kept.</param>
/// <param name="Differs">True when the other run has no such line: it gets a bar and the word "differs".</param>
internal sealed record CompareLineViewModel(string Text, bool Differs);
