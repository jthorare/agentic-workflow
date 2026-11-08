namespace Aw.Sindri.Core.Todos.Note;

/// <summary>One edit to the note's text. Numbers are the line numbers of the document the edits were made against.</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Number">The line number for Replace and Remove; 0 for Append.</param>
/// <param name="Line">The new line without a line ending for Replace and Append; null for Remove.</param>
public sealed record NoteLineEdit(NoteLineEditKind Kind, int Number, string? Line)
{
    /// <summary>Replaces a line.</summary>
    /// <param name="number">Its number.</param>
    /// <param name="line">The new line.</param>
    /// <returns>The edit.</returns>
    public static NoteLineEdit Replace(int number, string line) => new(NoteLineEditKind.Replace, number, line);

    /// <summary>Removes a line.</summary>
    /// <param name="number">Its number.</param>
    /// <returns>The edit.</returns>
    public static NoteLineEdit Remove(int number) => new(NoteLineEditKind.Remove, number, Line: null);

    /// <summary>Adds a todo line after the last todo line.</summary>
    /// <param name="line">The new line.</param>
    /// <returns>The edit.</returns>
    public static NoteLineEdit Append(string line) => new(NoteLineEditKind.Append, 0, line);
}
