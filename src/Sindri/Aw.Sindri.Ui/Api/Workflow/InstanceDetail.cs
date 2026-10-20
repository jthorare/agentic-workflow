namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /workflow/instances/{id}: one instance in full.</summary>
/// <param name="Address">Address, as served.</param>
/// <param name="Id">Id, as served.</param>
/// <param name="Parent">Parent, as served.</param>
/// <param name="ItemId">ItemId, as served.</param>
/// <param name="Title">Title, as served.</param>
/// <param name="Recipe">Recipe, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Since">Since, as served.</param>
/// <param name="EngineOwned">EngineOwned, as served.</param>
/// <param name="Subject">Subject, as served.</param>
/// <param name="OrderedBy">OrderedBy, as served.</param>
/// <param name="States">States, as served.</param>
/// <param name="Current">Current, as served.</param>
/// <param name="Facts">Facts, as served.</param>
/// <param name="Children">Children, as served.</param>
/// <param name="Counts">Counts, as served.</param>
/// <param name="Actions">Actions, as served.</param>
/// <param name="StepRuns">StepRuns, as served.</param>
/// <param name="AsOf">AsOf, as served.</param>
/// <param name="DisplayName">The workflow's display name when the instance view serves it; null otherwise.</param>
internal sealed record InstanceDetail(
    string Address,
    string Id,
    ParentRef? Parent,
    string? ItemId,
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
    IReadOnlyList<string> Actions,
    IReadOnlyList<StepRun> StepRuns,
    DateTimeOffset AsOf,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null);
