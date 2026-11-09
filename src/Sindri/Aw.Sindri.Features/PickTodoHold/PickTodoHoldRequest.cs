using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.PickTodoHold;

/// <summary>Apply the note's held changes or put them back (POST /sindri/todos/note/hold/apply and /restore; todo spec REQ-E7, §4.4): request class, so an agent session may pick too.</summary>
/// <param name="Restore">True to put the lines back in the note, false to apply the changes in Sindri.</param>
/// <param name="NoteHash">The note hash the row the caller saw was drawn from.</param>
/// <param name="Piped">Whether the request came with fence=1: the event records 'via: pipe'.</param>
public sealed record PickTodoHoldRequest(bool Restore, string NoteHash, bool Piped = false) : ICommand<PickTodoHoldResponse>, IClassifiedCommand
{
    /// <inheritdoc/>
    public string? Variant => Restore ? "restore" : "apply";

    /// <inheritdoc/>
    public bool CarriesProtectedFields => false;
}
