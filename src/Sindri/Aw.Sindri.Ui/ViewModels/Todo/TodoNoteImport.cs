namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Whether the Sindri this app talks to serves note import (POST /sindri/todos/note/import).</summary>
internal static class TodoNoteImport
{
    /// <summary>False until the note sync change adds the import route; flip it there and the card offers Import again.</summary>
    internal const bool Served = false;
}
