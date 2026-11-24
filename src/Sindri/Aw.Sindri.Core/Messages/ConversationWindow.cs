namespace Aw.Sindri.Core.Messages;

/// <summary>A window of a conversation's top-level lines, oldest first, with the gap markers that touch it (messages spec §8.2, W10).</summary>
/// <param name="Lines">The lines, oldest first; a thread root carries its reply count, its replies are not in the window.</param>
/// <param name="Gaps">The gaps whose edge is a line of the window, oldest first.</param>
public sealed record ConversationWindow(IReadOnlyList<MessageLine> Lines, IReadOnlyList<GapMarker> Gaps);
