namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>Whether a pass may adopt the note's open lines before the owner has imported them.</summary>
internal enum PassMode
{
    /// <summary>A watch or manual pass: a first sync with open lines waits for the import.</summary>
    Normal,

    /// <summary>The import: the owner confirmed.</summary>
    Import,
}
