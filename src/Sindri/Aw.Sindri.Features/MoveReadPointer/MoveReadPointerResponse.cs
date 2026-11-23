using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.MoveReadPointer;

/// <summary>Where the pointer stands now (messages spec §8.2). A local write: no ledger event, so no sequence.</summary>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="ReadTo">Where the local read pointer stands.</param>
public sealed record MoveReadPointerResponse(string Conversation, MessageTs ReadTo);
