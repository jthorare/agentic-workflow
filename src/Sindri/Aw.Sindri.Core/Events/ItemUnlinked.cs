using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'item.unlinked' payload (Linear hub spec 2026-11-04 §6.1): the owner took an item's tracker link off. It folds into 'items' and 'item-aliases': the item's link and the alias row go.</summary>
/// <param name="CommandId">The command that unlinked it.</param>
/// <param name="Item">The item.</param>
/// <param name="Record">The record it was linked to.</param>
/// <param name="Alias">The record's human key ('ENG-12'), or null.</param>
/// <param name="By">Who unlinked it.</param>
public sealed record ItemUnlinked(CommandId CommandId, ItemId Item, RecordKey Record, ItemAlias? Alias, CallerClass By);
