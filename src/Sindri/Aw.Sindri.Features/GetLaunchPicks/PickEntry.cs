using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>One run's pick rows: where the run is, when its first pick row was written, and the 'model.picked' once it arrives.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Sequence">The ledger sequence of the run's first pick row.</param>
/// <param name="At">When that row was written.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The workflow instance.</param>
/// <param name="State">The Step's state id.</param>
/// <param name="Attempt">The Step run's number.</param>
/// <param name="Picked">The 'model.picked' record, or null while Jev is being asked.</param>
internal sealed record PickEntry(LaunchId Launch, long Sequence, DateTimeOffset At, ItemId Item, InstanceAddress Instance, string State, int Attempt, ModelPicked? Picked);
