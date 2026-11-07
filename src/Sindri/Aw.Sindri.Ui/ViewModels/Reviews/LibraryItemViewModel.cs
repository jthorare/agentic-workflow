namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One entry of a library errors section: a doc, a near-miss file or an unreadable folder, with its lines.</summary>
/// <param name="Name">The doc id, the file or the folder.</param>
/// <param name="Path">The doc's file; empty for a near miss or a folder (their name is the path).</param>
/// <param name="Lines">What is wrong, one line each ("SND-LIB-010: …", or a folder's reason).</param>
internal sealed record LibraryItemViewModel(string Name, string Path, IReadOnlyList<string> Lines);
