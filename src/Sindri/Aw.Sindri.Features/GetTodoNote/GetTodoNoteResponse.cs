using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>The note's state.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="State">'not set' until a note is set.</param>
public sealed record GetTodoNoteResponse(AsOf AsOf, string State);
