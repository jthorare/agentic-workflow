using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One instance for the Workflows frame (API spec section 3.8, E-20, E-23); the spec calls it InstanceView, a name the engine's status view holds.</summary>
/// <param name="Address">The CLI address in the engine form ("ITM-01JK3/project/1"; a child "ITM-01JK3/project/1/step.5"); the app deep-links to workflow/{address}.</param>
/// <param name="Id">The engine's WFI id.</param>
/// <param name="Parent">The parent instance, for a child.</param>
/// <param name="ItemId">The item id.</param>
/// <param name="Title">The title.</param>
/// <param name="Recipe">The recipe stamp.</param>
/// <param name="State">The current state.</param>
/// <param name="Since">When it entered the state.</param>
/// <param name="EngineOwned">The badge.</param>
/// <param name="Subject">The subject.</param>
/// <param name="OrderedBy">How the states were ordered.</param>
/// <param name="States">Every state in path order.</param>
/// <param name="Current">The expanded state.</param>
/// <param name="Facts">Inputs then outputs.</param>
/// <param name="Children">Child groups in path order.</param>
/// <param name="Counts">The tab counts.</param>
/// <param name="Actions">The action ids allowed now.</param>
/// <param name="StepRuns">Every Step run in start order, folded from the instance's ledger events.</param>
/// <param name="AsOf">When it was read.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstanceDetail(
    InstanceAddress Address,
    WorkflowInstanceId Id,
    ParentRef? Parent,
    ItemKey? ItemId,
    string Title,
    InstanceRecipe Recipe,
    string State,
    DateTimeOffset Since,
    EngineOwnedBadge? EngineOwned,
    SubjectRef Subject,
    PathOrdering OrderedBy,
    IReadOnlyList<StateCell> States,
    CurrentState Current,
    IReadOnlyList<FactRow> Facts,
    IReadOnlyList<ChildGroup> Children,
    ViewCounts Counts,
    IReadOnlyList<ActionId> Actions,
    IReadOnlyList<StepRun> StepRuns,
    DateTimeOffset AsOf);
