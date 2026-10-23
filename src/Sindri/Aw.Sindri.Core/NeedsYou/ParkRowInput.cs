using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>What a 'park' row is built from: the instance the row belongs to and the suspension the ledger row shows.</summary>
/// <param name="Address">The instance's address; the row's id and subject.</param>
/// <param name="Suspension">The suspension the ledger row shows. A budget park's grant is a placeholder (the ledger row has no recipe in the fold to halve the state's budget from), so its row carries the answer kind and action, and the status line has the real amount.</param>
/// <param name="Refusal">The recorded refusal (code and message) of a dispatch park's effect; null when the transition recorded none.</param>
/// <param name="Descendant">The deepest parked descendant of <paramref name="Address"/> when the root itself is not parked: <paramref name="Suspension"/> is then that descendant's, and the row points at it (`via: "child"`, `child`, a cli that answers it). Null for a park on the instance itself.</param>
public sealed record ParkRowInput(InstanceAddress Address, SuspensionSummary Suspension, string? Refusal = null, InstanceAddress? Descendant = null);
