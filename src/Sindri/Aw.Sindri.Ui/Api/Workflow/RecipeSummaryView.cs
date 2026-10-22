namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One row of GET /sindri/workflow/recipes: what the picker needs. The inputs come from the recipe's own read, not from here.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Version">The recipe version.</param>
/// <param name="Hash">The content hash, sha256:&lt;64 hex&gt;.</param>
/// <param name="Standing">'approved', 'unapproved' or 'revoked', as served; only an approved recipe starts.</param>
/// <param name="Name">The recipe's name template as written, or null.</param>
/// <param name="DisplayName">What the recipe does, in a sentence; read as optional (an older Sindri serves none).</param>
/// <param name="Stale">Why an approved recipe is out of date: pin-drift, child-stale or legacy-shape; empty when it is current. Optional on read.</param>
/// <param name="Inputs">The declared inputs, as the list serves them (the Recipes page counts them). Optional on read.</param>
/// <param name="Outputs">The declared outputs (the page counts them). Optional on read.</param>
/// <param name="States">The states, in order (the page counts them). Optional on read.</param>
internal sealed record RecipeSummaryView(string Id, int Version, string Hash, string Standing, string? Name, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Stale = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeInputView>? Inputs = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeOutputView>? Outputs = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeStateView>? States = null);
