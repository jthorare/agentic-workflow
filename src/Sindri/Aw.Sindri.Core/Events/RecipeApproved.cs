using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'recipe.approved' payload (spec §4.1), keyed 'recipe.approved:&lt;hash&gt;:after:&lt;seq&gt;' so a drift approval of the same hash is its own event.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The approved hash.</param>
/// <param name="Via">Desk or freeze.</param>
/// <param name="Base">The base of a generated recipe, or null.</param>
/// <param name="Pins">The pins recorded.</param>
/// <param name="Drift">True for a drift approval (the same hash re-approved with new pins).</param>
/// <param name="Batch">The approve-all command id when one hold approved this recipe with others (setup-once spec §7.5), else null.</param>
/// <param name="PlanDigest">The approve-all plan's digest, else null.</param>
/// <param name="HeldMs">How long the owner held the confirmation for, when it was held, else null.</param>
public sealed record RecipeApproved(RecipeId Id, int Version, RecipeHash Hash, ApprovalVia Via, RecipeRef? Base, RecipePins Pins, bool Drift,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Batch = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PlanDigest = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? HeldMs = null);
