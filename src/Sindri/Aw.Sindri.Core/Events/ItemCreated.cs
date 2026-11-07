using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'item.created' payload (API spec §3.4): a human created a work item. Its public form, <see cref="ItemCreatedPublic"/>, leaves the body out (§5.3).</summary>
/// <param name="CommandId">The 'CreateItem' command's id.</param>
/// <param name="Item">The new item.</param>
/// <param name="Title">Its title, as the human typed it (the projection scrubs and escapes it).</param>
/// <param name="Repo">The repo it belongs to, or null.</param>
/// <param name="Link">The tracker record it was created from, or null (lead answer 1: no separate 'item.linked' on create).</param>
/// <param name="Body">Its body, or null; never public.</param>
/// <param name="Owner">Reserved for multi-user (README X61); always null for now.</param>
/// <param name="By">Who made it (Linear hub spec 2026-11-04 §6.1): the caller class. An event written before the field replays with null, which counts as not the owner.</param>
/// <param name="LinkAlias">The linked record's human key ('ENG-12'), or null: the 'item-aliases' view folds it with <paramref name="Link"/>.</param>
public sealed record ItemCreated(CommandId CommandId, ItemId Item, string Title, RepoName? Repo, RecordKey? Link, string? Body, string? Owner, CallerClass? By = null, ItemAlias? LinkAlias = null);
