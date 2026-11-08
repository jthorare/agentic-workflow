using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Todos;

/// <summary>A both-places conflict a todo's row carries until a caller picks a side (todo spec D11).</summary>
/// <param name="Sindri">Sindri's version.</param>
/// <param name="Obsidian">The note's version.</param>
/// <param name="NoteHash">The sha256 of the note the conflict was found in.</param>
public sealed record TodoOpenConflict(TodoSide Sindri, TodoSide Obsidian, string NoteHash);
