using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>What 'recipe revoke' previews (spec §4.2): the instances that will park, the takeovers it ends, and the generated recipes the cascade revokes.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The hash to revoke.</param>
/// <param name="Parks">The running instances that will park.</param>
/// <param name="TakeoversEnded">The instances whose takeover the revoke ends.</param>
/// <param name="Cascade">The generated recipes approved from it, which are revoked too.</param>
/// <param name="Parents">The approved recipes that pin it as a child: the revoke marks them child-stale (spec §4.1).</param>
public sealed record RevokePreview(
    RecipeId Id, int Version, RecipeHash Hash, IReadOnlyList<WorkflowInstanceId> Parks, IReadOnlyList<WorkflowInstanceId> TakeoversEnded,
    IReadOnlyList<RecipeRef> Cascade, IReadOnlyList<RecipeRef> Parents);
