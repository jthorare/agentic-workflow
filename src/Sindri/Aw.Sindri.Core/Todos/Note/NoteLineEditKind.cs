namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What a <see cref="NoteLineEdit"/> does to the note.</summary>
public enum NoteLineEditKind
{
    /// <summary>Replaces the line at a number.</summary>
    Replace,

    /// <summary>Removes the line at a number, with its line ending.</summary>
    Remove,

    /// <summary>Adds a new todo line after the note's last todo line (at the end when it has none).</summary>
    Append,
}
