using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// What each setup step reads (GET /sindri/setup): one member per source, each a pure read that starts nothing. A source that can't be read is an
/// error here, and the service turns it into that step's "failed", so one bad source never hides the others.
/// </summary>
public interface ISetupFacts
{
    /// <summary>The repos the profile names.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fact, or the error that stopped the read.</returns>
    public ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken);

    /// <summary>Where the profile stands against its approval.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fact, or the error that stopped the read.</returns>
    public ValueTask<Result<ProfileFact>> Profile(CancellationToken cancellationToken);

    /// <summary>Where the registered recipes stand.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fact, or the error that stopped the read.</returns>
    public ValueTask<Result<RecipesFact>> Recipes(CancellationToken cancellationToken);

    /// <summary>Where the per-repo MCP registration stands.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fact, or the error that stopped the read.</returns>
    public ValueTask<Result<McpFact>> Mcp(CancellationToken cancellationToken);

    /// <summary>Where the context map stands.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fact, or the error that stopped the read.</returns>
    public ValueTask<Result<MapFact>> Map(CancellationToken cancellationToken);

    /// <summary>Where the managed Onyx stands.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fact, or the error that stopped the read.</returns>
    public ValueTask<Result<OnyxFact>> Onyx(CancellationToken cancellationToken);
}
