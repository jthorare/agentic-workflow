using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>A parked descendant of a root instance, kept on the root's 'park' row record so the row can describe the deepest one and name the next when that one resumes.</summary>
/// <param name="Address">The descendant's address.</param>
/// <param name="Suspension">The suspension its ledger row showed.</param>
/// <param name="Refusal">The refusal (code and message) a dispatch park recorded; null when none.</param>
/// <param name="Sequence">The ledger sequence of the row that parked it; the later of two equally deep descendants shows.</param>
public sealed record NestedPark(string Address, SuspensionSummary Suspension, string? Refusal, long Sequence);
