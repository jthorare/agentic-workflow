using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>A previously approved recipe that is approved on its own, typed, with ApproveRecipe (setup-once spec §5.3, §7.2).</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name shown instead of the id.</param>
/// <param name="Reason">Why it is not in the hold.</param>
/// <param name="Sentence">The plain sentence the owner reads.</param>
/// <param name="Added">The capabilities it gained; empty when it only waits for a recipe it uses.</param>
public sealed record OwnApprovalRow(RecipeId Id, string DisplayName, OwnApprovalReason Reason, string Sentence, IReadOnlyList<CapabilityLine> Added)
{
    /// <summary>The hash of the shipped version that waits; the row's CLI line is 'recipe approve &lt;id&gt;@&lt;hash&gt;'. Null when not known, and left out of the JSON.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RecipeHash? Hash { get; init; }

    /// <summary>The shipped version that waits; null when not known, and left out of the JSON.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Version { get; init; }
}
