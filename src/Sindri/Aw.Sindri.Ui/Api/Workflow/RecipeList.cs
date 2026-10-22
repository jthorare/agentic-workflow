namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /sindri/workflow/recipes: every registered recipe, whatever its standing.</summary>
/// <param name="Recipes">The rows in the API's order.</param>
/// <param name="Window">The window the rows' stats cover: '7d', '30d' or '90d'; optional on read (an older Sindri serves none).</param>
internal sealed record RecipeList(IReadOnlyList<RecipeSummaryView> Recipes,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Window = null);
