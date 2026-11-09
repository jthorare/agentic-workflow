using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'item.handoff' payload (#2258 PR 12): an outside system or a local script handed Sindri work. It follows the 'item.created' of the item it made and
/// keeps the whole offer, so the prompt does not depend on the item body's 4,000-character limit. Never public: the prompt is untrusted text.
/// </summary>
/// <param name="CommandId">The 'CreateHandoffItem' command's id.</param>
/// <param name="Item">The item the handoff made.</param>
/// <param name="Offer">The sender's id for the offer; the item id when the sender gave none.</param>
/// <param name="Source">Who sent it ('local', a script name or a connection name); the envelope's actor is 'handoff:' and this.</param>
/// <param name="Repo">The repo the work is for, or null.</param>
/// <param name="Branch">The branch it names, or null.</param>
/// <param name="Prompt">The full prompt, or null.</param>
/// <param name="Links">Links the sender gave.</param>
/// <param name="Labels">Labels the sender gave.</param>
public sealed record ItemHandoff(CommandId CommandId, ItemId Item, string Offer, string Source, RepoName? Repo, string? Branch, string? Prompt, IReadOnlyList<string> Links, IReadOnlyList<string> Labels);
