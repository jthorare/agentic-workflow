namespace Aw.Sindri.Core.Forge;

/// <summary>A pull request a live workflow instance waits on (PR babysitter spec §6.2): the link 'fact:pr' holds and the item the instance runs for, which 'pr.changed' routes by until E3.</summary>
/// <param name="Link">The pull request's link, exactly as 'fact:pr' holds it; it is the event's 'subject'.</param>
/// <param name="Item">The item key of the waiting instance.</param>
public sealed record WatchedPull(string Link, string Item);
