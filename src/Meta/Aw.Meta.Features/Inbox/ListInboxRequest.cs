namespace Aw.Meta.Features.Inbox;

/// <summary>The ListInbox request.</summary>
/// <param name="All">Whether to list every message; otherwise only those still owed a reply.</param>
/// <param name="Once">Whether to list only open messages the inbox has not shown yet (a hook's view), marking them shown.</param>
public sealed record ListInboxRequest(bool All, bool Once = false);
