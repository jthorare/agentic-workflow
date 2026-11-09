namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/todos/note/import's answer (todo spec §4.4): what the import adopted. Guessed shape.</summary>
/// <param name="Imported">The tasks that got an id.</param>
/// <param name="CantImport">The tasks left as text.</param>
internal sealed record TodoNoteImportedView(int Imported, int CantImport);
