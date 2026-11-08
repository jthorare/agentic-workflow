using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// What an item row keeps of its root instance, folded from the 'workflow.*' ledger rows (API P11), so a row whose delta leaves
/// the frame out still knows the frame that came before.
/// </summary>
/// <param name="Address">The root instance's address.</param>
/// <param name="State">The instance's state.</param>
/// <param name="RunOpen">Whether a run is open in the state.</param>
/// <param name="Runtime">Who runs the open run; null when none is open.</param>
/// <param name="Suspension">The suspension the instance is under, or none.</param>
/// <param name="Ended">Whether the instance ended.</param>
/// <param name="Cancelled">Whether it ended by a cancel.</param>
public sealed record EngineItemPart(string Address, string State, bool RunOpen, StepRuntime? Runtime, SuspensionKind? Suspension, bool Ended, bool Cancelled)
{
    /// <summary>Why the instance is parked, when <see cref="Suspension"/> is a park; the health count of unavailable parks reads it. Not written when there is none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ParkReason? Reason { get; init; }

    /// <summary>
    /// The item's other root instances that are still live (a second recipe started on the item), each as this record without its own
    /// <see cref="Earlier"/>. The item's state follows this part, the latest root; these keep their own needs-you rows settling and keep
    /// the item from being closed under them. Not written when there are none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EngineItemPart>? Earlier { get; init; }
}
