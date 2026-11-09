using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'item.linked' payload (API spec §3.4, README X42): an existing item was linked to a tracker record (written by context-sync later).</summary>
/// <param name="CommandId">The command that linked it.</param>
/// <param name="Item">The item.</param>
/// <param name="Record">The tracker record.</param>
/// <param name="Alias">The record's human key ('ENG-142'), or null.</param>
/// <param name="Url">Where the record lives (a pull request's page), written when known; null otherwise and for events from before it existed.</param>
public sealed record ItemLinked(CommandId CommandId, ItemId Item, RecordKey Record, ItemAlias? Alias, string? Url = null);
