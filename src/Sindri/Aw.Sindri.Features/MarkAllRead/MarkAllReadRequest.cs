using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.MarkAllRead;

/// <summary>
/// Mark all read and its Undo (POST /sindri/messages/lists/unread/read and /unread; messages spec §8.2): moves the local pointers in one transaction,
/// sends nothing to Slack and writes no Home event. The catalog variant is 'read' or 'unread' (the Undo).
/// </summary>
/// <param name="Conversations">The pointers to set, one per conversation.</param>
/// <param name="Undo">True for the Undo (the unread route).</param>
public sealed record MarkAllReadRequest(IReadOnlyList<ReadPointer> Conversations, bool Undo) : ICommand<MarkAllReadResponse>, IClassifiedCommand
{
    /// <inheritdoc/>
    public string Variant => Undo ? "unread" : "read";

    /// <inheritdoc/>
    public bool CarriesProtectedFields => false;
}
