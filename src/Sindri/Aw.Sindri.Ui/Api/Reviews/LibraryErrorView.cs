namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One error of a doc.</summary>
/// <param name="Rule">The SND-LIB rule.</param>
/// <param name="Text">Sindri's words, with the fix.</param>
internal sealed record LibraryErrorView(string Rule, string Text);
