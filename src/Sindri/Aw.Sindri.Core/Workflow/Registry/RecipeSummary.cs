using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>One recipe line (engine spec §6.3 'recipe list'; README X34: with its inputs, for the app's start form).</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name shown instead of the id (setup-once spec §7.7); null, and left out of the JSON, when the engine knows none yet (the field is required on a recipe file once RCP-DISPLAY-001 lands). The app makes the id readable itself.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
/// <param name="Standing">Unapproved, approved or revoked.</param>
/// <param name="Via">How its latest approval was given, or null when unapproved.</param>
/// <param name="Stale">Why it is stale (empty when it isn't).</param>
/// <param name="Inputs">The facts a start supplies (the first is the subject), with their types and kinds.</param>
/// <param name="Urgency">The recipe's urgency: incident, triage or work.</param>
/// <param name="Outputs">What the recipe produces for a human, each with the state it comes from.</param>
/// <param name="OrderedBy">'path', or 'transition-list' when 'initial' has more than one first target (spec §3.10).</param>
/// <param name="States">Every state with its path order and branch (spec §3.10, UI E-20); computed, never hashed.</param>
/// <param name="Loops">The loop edges of the path walk (spec §3.10).</param>
/// <param name="Name">The recipe's name template as written, or null (spec §3.11).</param>
public sealed record RecipeSummary(RecipeId Id, int Version, RecipeHash Hash, RecipeStanding Standing, ApprovalVia? Via, IReadOnlyList<StaleReason> Stale, IReadOnlyList<RecipeInput> Inputs, string Urgency, IReadOnlyList<RecipeOutput> Outputs, string OrderedBy, IReadOnlyList<StatePlace> States, IReadOnlyList<PathLoop> Loops, string? Name = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null)
{
    /// <summary>The list's status word (API spec §3.8, E-25): 'approved', 'changed', 'draft' or 'revoked'; set by the list, null and left out of the JSON elsewhere.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; init; }

    /// <summary>What changed, for a 'changed' recipe; left out of the JSON otherwise.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusDetail { get; init; }

    /// <summary>True for a recipe that only ever runs as a child; set by the list, left out of the JSON elsewhere.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? ChildOnly { get; init; }

    /// <summary>The recipe's stats at the list's window; left out of the JSON for a draft, a child-only recipe and a list read without them.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Stats.RecipeListStats? Stats { get; init; }
}
