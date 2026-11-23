using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.MoveReadPointer;

/// <summary>
/// Moves a conversation's local read pointer (POST /sindri/messages/conversations/{id}/read and /unread, 'sindri messages read|unread'; messages spec §8.2):
/// 'e' in Unread and 'u'. Release 1 sends nothing to Slack and writes no Home event. The catalog variant is the direction.
/// </summary>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="Ts">The ts the pointer moves to.</param>
/// <param name="Move">Read or unread.</param>
public sealed record MoveReadPointerRequest(string Conversation, MessageTs Ts, ReadPointerMove Move) : ICommand<MoveReadPointerResponse>, IClassifiedCommand
{
    /// <inheritdoc/>
    public string Variant => Move == ReadPointerMove.Unread ? "unread" : "read";

    /// <inheritdoc/>
    public bool CarriesProtectedFields => false;
}
