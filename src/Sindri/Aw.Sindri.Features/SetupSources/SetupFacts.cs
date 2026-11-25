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
/// <param name="builds">The running and installed builds (first-run spec §3.2).</param>
internal sealed class SetupFacts(ProfileSetupReader profile, SourcesSetupReader sources, McpSetupReader mcp, MapSetupReader map, IBuildStamps builds) : ISetupFacts
{
    public ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken) => profile.Repos(cancellationToken);

    public ValueTask<Result<ProfileFact>> Profile(CancellationToken cancellationToken) => profile.Presence(cancellationToken);

    public ValueTask<Result<McpFact>> Mcp(CancellationToken cancellationToken) => mcp.Read(cancellationToken);

    public ValueTask<Result<MapFact>> Map(CancellationToken cancellationToken) => map.Read(cancellationToken);

    public ValueTask<Result<SourcesFact>> Sources(CancellationToken cancellationToken) => sources.Read(cancellationToken);

    // Current unless both stamps are known and differ: a dev run or a test has none (first-run spec A5).
    public ValueTask<Result<ServeFact>> Serve(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ServeFact>>(new ServeFact(builds.Running() is not { } running || builds.Installed() is not { } installed || string.Equals(running, installed, StringComparison.Ordinal))
        {
            Running = builds.RunningBuild(),
            Installed = builds.InstalledBuild(),
        });

    public ValueTask<Result<AgentFact>> Agent(CancellationToken cancellationToken) => profile.Agent(cancellationToken);

    public ValueTask<Result<FirstWorkflowFact>> FirstWorkflow(CancellationToken cancellationToken) => profile.FirstWorkflow(cancellationToken);
}
