namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Whether the Sindri this app talks to serves note import (POST /sindri/todos/note/import).</summary>
internal static class TodoNoteImport
{
    /// <summary>True: the note sync change serves the import route, so the card offers Import and start syncing.</summary>
    internal const bool Served = true;
}
