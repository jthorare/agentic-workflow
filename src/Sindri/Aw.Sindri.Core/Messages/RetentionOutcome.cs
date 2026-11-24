namespace Aw.Sindri.Core.Messages;

/// <summary>What one retention pass removed from messages.db (messages spec §8.1).</summary>
/// <param name="MessagesDeleted">Messages deleted, with their FTS rows.</param>
/// <param name="OutboxDeleted">Landed or discarded outbox rows deleted.</param>
/// <param name="TextExpired">Kept threads whose skip ended at the 180-day cap: their rows now say "text expired".</param>
public sealed record RetentionOutcome(int MessagesDeleted, int OutboxDeleted, int TextExpired);
