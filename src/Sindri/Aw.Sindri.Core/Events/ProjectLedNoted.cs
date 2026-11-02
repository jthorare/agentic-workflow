using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'project.led.noted' payload (projects page spec §5.1, owner q158): the owner wrote, replaced or cleared the notes on a Linear project they lead, which has no 'project.attached' row to carry them. It shares the hide row's version with 'project.hidden'.</summary>
/// <param name="CommandId">The command that did it.</param>
/// <param name="Linear">The led Linear project ('linear:project/&lt;id&gt;').</param>
/// <param name="Notes">The new notes (at most 16,384 characters), or null when the owner cleared them.</param>
/// <param name="Version">The card's version after this change: the same counter 'project.hidden' moves, so a led project never noted or hidden is at 0 and the first of either is 1.</param>
public sealed record ProjectLedNoted(CommandId CommandId, string Linear, string? Notes, long Version);
