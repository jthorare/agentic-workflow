namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The owner's flags on a thread (messages spec §8.1, the owner columns of thread_state): Later and follow. They are owner state, so a re-sync and a plain
/// purge keep them, and retention keeps a flagged thread's text for up to 180 days.
/// </summary>
/// <param name="Conversation">The conversation.</param>
/// <param name="Thread">The thread root's ts; null for a DM, whose row is the conversation.</param>
/// <param name="Later">The Later flag.</param>
/// <param name="Follow">The follow flag.</param>
public sealed record ThreadOwnerState(string Conversation, MessageTs? Thread, bool Later, bool Follow);
