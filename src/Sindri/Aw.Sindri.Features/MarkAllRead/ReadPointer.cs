using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.MarkAllRead;

/// <summary>One conversation's pointer in a Mark all read: '{id, ts}' on the wire.</summary>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="Ts">The ts the pointer moves to.</param>
public sealed record ReadPointer(string Conversation, MessageTs Ts);
