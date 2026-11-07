namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>A file that almost reads as a doc.</summary>
/// <param name="Path">The file.</param>
/// <param name="Rule">The SND-LIB rule.</param>
/// <param name="Text">Sindri's words, with the fix.</param>
internal sealed record LibraryNearMissView(string Path, string Rule, string Text);
