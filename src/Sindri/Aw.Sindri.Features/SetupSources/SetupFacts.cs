using Aw.Kernel;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// <see cref="ISetupFacts"/> over what Sindri already keeps: the profile (live and approved), the recipe registry, Claude Code's config, the context engine and its build jobs, and the
/// Onyx status. Every member is a read; none of them starts, registers or approves anything.
/// </summary>
/// <param name="profile">The profile reads.</param>
/// <param name="recipes">The recipe registry.</param>
/// <param name="shipped">The recipes this build ships, whose plan says which are approved.</param>
/// <param name="onyx">The Onyx status, the owner's "Not now" and the setup job.</param>
/// <param name="mcp">The MCP registration read.</param>
/// <param name="map">The map read.</param>
internal sealed class SetupFacts(ProfileSetupReader profile, RecipeRegistryReader recipes, IShippedRecipes shipped, OnyxSetupReader onyx, McpSetupReader mcp, MapSetupReader map) : ISetupFacts
{
    public ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken) => profile.Repos(cancellationToken);

    public ValueTask<Result<ProfileFact>> Profile(CancellationToken cancellationToken) => profile.Presence(cancellationToken);

    public async ValueTask<Result<RecipesFact>> Recipes(CancellationToken cancellationToken)
    {
        var lines = await recipes.Summaries(cancellationToken).ConfigureAwait(false);
        var plan = lines.IsError ? lines.Error : await shipped.Plan(cancellationToken).ConfigureAwait(false);
        return plan.IsError ? plan.Error : SetupRecipes.Count(lines.Value, plan.Value);
    }

    public ValueTask<Result<McpFact>> Mcp(CancellationToken cancellationToken) => mcp.Read(cancellationToken);

    public ValueTask<Result<MapFact>> Map(CancellationToken cancellationToken) => map.Read(cancellationToken);

    public ValueTask<Result<OnyxFact>> Onyx(CancellationToken cancellationToken) => onyx.Read(cancellationToken);
}
