namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The three kinds of change the hold counts (todo spec §4.5 "The hold").</summary>
public enum HeldKind
{
    /// <summary>An open todo's line is gone.</summary>
    Removed,

    /// <summary>An open todo's text, due date or links changed.</summary>
    Edited,

    /// <summary>A done todo's line went back to '[ ]'.</summary>
    Reopened,
}
