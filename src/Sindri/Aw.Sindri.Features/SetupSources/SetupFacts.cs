using Aw.Kernel;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// <see cref="ISetupFacts"/> over what Sindri already keeps: the profile (live and approved), Claude Code's config, the context engine and its build jobs, and the
/// index sources' token pointers. Every member is a read; none of them starts, registers or approves anything.
/// </summary>
/// <param name="profile">The profile reads.</param>
/// <param name="sources">The index sources read.</param>
/// <param name="mcp">The MCP registration read.</param>
/// <param name="map">The map read.</param>
internal sealed class SetupFacts(ProfileSetupReader profile, SourcesSetupReader sources, McpSetupReader mcp, MapSetupReader map) : ISetupFacts
{
    public ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken) => profile.Repos(cancellationToken);

    public ValueTask<Result<ProfileFact>> Profile(CancellationToken cancellationToken) => profile.Presence(cancellationToken);

    public ValueTask<Result<McpFact>> Mcp(CancellationToken cancellationToken) => mcp.Read(cancellationToken);

    public ValueTask<Result<MapFact>> Map(CancellationToken cancellationToken) => map.Read(cancellationToken);

    public ValueTask<Result<SourcesFact>> Sources(CancellationToken cancellationToken) => sources.Read(cancellationToken);

    // Until the build-info compare (first-run spec A5) there is no stamp to differ on, so serve counts as current.
    public ValueTask<Result<ServeFact>> Serve(CancellationToken cancellationToken) => ValueTask.FromResult<Result<ServeFact>>(new ServeFact(Current: true));

    public ValueTask<Result<AgentFact>> Agent(CancellationToken cancellationToken) => profile.Agent(cancellationToken);

    public ValueTask<Result<FirstWorkflowFact>> FirstWorkflow(CancellationToken cancellationToken) => profile.FirstWorkflow(cancellationToken);
}
