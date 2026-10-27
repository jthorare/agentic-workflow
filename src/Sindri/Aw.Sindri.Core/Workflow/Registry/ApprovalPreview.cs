using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// What 'recipe approve' and 'recipe show' preview (spec §4.2): the recipe, the diff against the last approved
/// version, the capability uses, the effect kinds, the pins, and for a drift approval the changed blocks and the
/// parked instances the approval resumes. The eval result joins from P4.
/// </summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
/// <param name="Base">The base of a generated recipe, or null.</param>
/// <param name="Diff">A unified diff of the normalized text against the last approved version, or null when none was approved.</param>
/// <param name="CapabilityUses">The capability uses, in transition order.</param>
/// <param name="Effects">The effect kinds and their allowlist status.</param>
/// <param name="Pins">What the approval will pin.</param>
/// <param name="Changed">For a drift approval, the pinned blocks that changed; else empty.</param>
/// <param name="Resumes">For a drift approval, the instances parked on that drift which it resumes; else empty.</param>
/// <param name="Evals">The eval result line (from P4 on), or null.</param>
public sealed record ApprovalPreview(
    RecipeId Id, int Version, RecipeHash Hash, RecipeRef? Base, string? Diff,
    IReadOnlyList<PreviewCapabilityUse> CapabilityUses, IReadOnlyList<PreviewEffect> Effects, RecipePins Pins,
    IReadOnlyList<PreviewBlockChange> Changed, IReadOnlyList<WorkflowInstanceId> Resumes, string? Evals)
{
    /// <summary>Whether this is a drift approval's preview: the hash is approved and some pinned block changed.</summary>
    public bool IsDrift => Changed.Count > 0;

    /// <summary>Each child the recipe uses, with the version pinned and a warning when a newer shipped one waits (#1042); null, and left out of the JSON, when it uses none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PreviewChildNote>? ChildNotes { get; init; }
}
