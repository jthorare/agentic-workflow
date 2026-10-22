using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory ISetupFacts: each source answers what the test set (a fresh box: nothing done), kept honest by SetupFactsContract.</summary>
public sealed class InMemorySetupFacts : ISetupFacts
{
    /// <summary>Gets or sets the repos' answer.</summary>
    public Result<RepoFact> RepoAnswer { get; set; } = new RepoFact(0);

    /// <summary>Gets or sets the profile's answer.</summary>
    public Result<ProfileFact> ProfileAnswer { get; set; } = new ProfileFact(ProfilePresence.Missing);

    /// <summary>Gets or sets the recipes' answer.</summary>
    public Result<RecipesFact> RecipesAnswer { get; set; } = new RecipesFact(0, 0);

    /// <summary>Gets or sets the MCP registration's answer.</summary>
    public Result<McpFact> McpAnswer { get; set; } = new McpFact(0, 0, "Claude Code");

    /// <summary>Gets or sets the map's answer.</summary>
    public Result<MapFact> MapAnswer { get; set; } = new MapFact(MapPhase.Waiting);

    /// <summary>Gets or sets Onyx's answer.</summary>
    public Result<OnyxFact> OnyxAnswer { get; set; } = new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null);

    /// <summary>The profile names one repo, demo.</summary>
    public static RepoFact OneRepo { get; } = new(1) { Items = new SetupItems([new SetupItem("demo", SetupState.Done, Detail: null)]) };

    /// <summary>Everything done: a repo, an approved profile, every recipe approved, the registration made, the map built and Onyx up.</summary>
    /// <returns>The fake.</returns>
    public static InMemorySetupFacts AllDone() => new()
    {
        RepoAnswer = OneRepo,
        ProfileAnswer = new ProfileFact(ProfilePresence.Approved),
        RecipesAnswer = new RecipesFact(26, 26),
        McpAnswer = new McpFact(1, 1, "Claude Code") { Items = new SetupItems([new SetupItem("demo", SetupState.Done, "Claude Code") { Providers = [new McpProviderState("claude", McpConnection.Connected)] }]) },
        MapAnswer = new MapFact(MapPhase.Built) { Items = new SetupItems([new SetupItem("demo", SetupState.Done, Detail: null)]) },
        OnyxAnswer = new OnyxFact(OnyxState.Running, Unreachable: false, BlockedByCode: null),
    };

    /// <inheritdoc/>
    public ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken) => ValueTask.FromResult(RepoAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<ProfileFact>> Profile(CancellationToken cancellationToken) => ValueTask.FromResult(ProfileAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<RecipesFact>> Recipes(CancellationToken cancellationToken) => ValueTask.FromResult(RecipesAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<McpFact>> Mcp(CancellationToken cancellationToken) => ValueTask.FromResult(McpAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<MapFact>> Map(CancellationToken cancellationToken) => ValueTask.FromResult(MapAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<OnyxFact>> Onyx(CancellationToken cancellationToken) => ValueTask.FromResult(OnyxAnswer);
}
