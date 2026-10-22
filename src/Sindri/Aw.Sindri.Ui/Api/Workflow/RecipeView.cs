namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /workflow/recipes/{id}: what a recipe declares.</summary>
/// <param name="Id">The recipe id (the body starts with it).</param>
/// <param name="Hash">The recipe's content hash, sha256:&lt;64 hex&gt;.</param>
/// <param name="Version">The recipe version.</param>
/// <param name="Name">The recipe's name template as written, or null.</param>
/// <param name="Inputs">Inputs, as served.</param>
/// <param name="Outputs">Outputs, as served.</param>
/// <param name="Urgency">Urgency, as served.</param>
/// <param name="OrderedBy">OrderedBy, as served.</param>
/// <param name="States">States, as served.</param>
/// <param name="Loops">Loops, as served.</param>
/// <param name="DisplayName">What the recipe does, in a sentence; read as optional (an older Sindri serves none).</param>
/// <param name="TransitionLabels">The plain-word label of an edge; read as optional (an older Sindri serves none).</param>
internal sealed record RecipeView(
    string Id,
    string Hash,
    int Version,
    string? Name,
    IReadOnlyList<RecipeInputView> Inputs,
    IReadOnlyList<RecipeOutputView> Outputs,
    RecipeUrgency Urgency,
    PathOrdering OrderedBy,
    IReadOnlyList<RecipeStateView> States,
    IReadOnlyList<RecipeLoopView> Loops,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeTransitionLabelView>? TransitionLabels = null);
