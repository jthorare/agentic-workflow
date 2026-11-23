namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One file the engine returned for a question: where it is and why the engine named it (#2974).</summary>
/// <param name="Path">The full repo path.</param>
/// <param name="Directory">The directory part with its trailing slash, drawn dimmed; empty for a file at the root.</param>
/// <param name="Name">The file name, drawn in full strength.</param>
/// <param name="Reason">The engine's reason for the file.</param>
internal sealed record AnswerFile(string Path, string Directory, string Name, string Reason);
