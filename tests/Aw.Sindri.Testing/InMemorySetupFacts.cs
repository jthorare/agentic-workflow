using Aw.Kernel;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory ISetupFacts: each source answers what the test set (a fresh box: nothing done), kept honest by SetupFactsContract.</summary>
public sealed class InMemorySetupFacts : ISetupFacts
{
    /// <summary>Gets or sets the repos' answer.</summary>
    public Result<RepoFact> RepoAnswer { get; set; } = new RepoFact(0);

    /// <summary>Gets or sets the profile's answer.</summary>
    public Result<ProfileFact> ProfileAnswer { get; set; } = new ProfileFact(ProfilePresence.Missing);

    /// <summary>Gets or sets the MCP registration's answer.</summary>
    public Result<McpFact> McpAnswer { get; set; } = new McpFact(0, 0, "Claude Code");

    /// <summary>Gets or sets the map's answer.</summary>
    public Result<MapFact> MapAnswer { get; set; } = new MapFact(MapPhase.Waiting);

    /// <summary>Gets or sets the index sources' answer.</summary>
    public Result<SourcesFact> SourcesAnswer { get; set; } = new SourcesFact(0);

    /// <summary>Gets or sets the serve step's answer (the running Sindri is the installed build).</summary>
    public Result<ServeFact> ServeAnswer { get; set; } = new ServeFact(Current: true);

    /// <summary>Gets or sets the agent step's answer (none set).</summary>
    public Result<AgentFact> AgentAnswer { get; set; } = new AgentFact(Target: null);

    /// <summary>Gets or sets the hello-sindri step's answer (not run, not dismissed).</summary>
    public Result<FirstWorkflowFact> FirstWorkflowAnswer { get; set; } = new FirstWorkflowFact(Succeeded: false, Dismissed: false);

    /// <summary>The profile names one repo, demo.</summary>
    public static RepoFact OneRepo { get; } = new(1) { Items = new SetupItems([new SetupItem("demo", SetupState.Done, Detail: null)]) };

    /// <summary>Everything done: serve current, a repo, an approved profile, an agent set, the registration made, the map built, every named source connected and hello-sindri run once.</summary>
    /// <returns>The fake.</returns>
    public static InMemorySetupFacts AllDone() => new()
    {
        RepoAnswer = OneRepo,
        ProfileAnswer = new ProfileFact(ProfilePresence.Present),
        McpAnswer = new McpFact(1, 1, "Claude Code") { Items = new SetupItems([new SetupItem("demo", SetupState.Done, "Claude Code") { Providers = [new McpProviderState("claude", McpConnection.Connected)] }]) },
        MapAnswer = new MapFact(MapPhase.Built) { Items = new SetupItems([new SetupItem("demo", SetupState.Done, Detail: null)]) },
        SourcesAnswer = NamedSources(("github", true), ("slack", true)),
        AgentAnswer = new AgentFact("claude-code"),
        FirstWorkflowAnswer = new FirstWorkflowFact(Succeeded: true, Dismissed: false),
    };

    /// <summary>The sources fact for the named sources, each connected or not.</summary>
    /// <param name="named">The source names with whether each token resolves.</param>
    /// <returns>The fact.</returns>
    public static SourcesFact NamedSources(params (string Source, bool Connected)[] named) => new(named.Length)
    {
        Items = new SetupItems([.. named.Select(static n => new SetupItem(n.Source, n.Connected ? SetupState.Done : SetupState.Todo, n.Connected ? "connected" : "needs token"))]),
    };

    /// <inheritdoc/>
    public ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken) => ValueTask.FromResult(RepoAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<ProfileFact>> Profile(CancellationToken cancellationToken) => ValueTask.FromResult(ProfileAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<McpFact>> Mcp(CancellationToken cancellationToken) => ValueTask.FromResult(McpAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<MapFact>> Map(CancellationToken cancellationToken) => ValueTask.FromResult(MapAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<SourcesFact>> Sources(CancellationToken cancellationToken) => ValueTask.FromResult(SourcesAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<ServeFact>> Serve(CancellationToken cancellationToken) => ValueTask.FromResult(ServeAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<AgentFact>> Agent(CancellationToken cancellationToken) => ValueTask.FromResult(AgentAnswer);

    /// <inheritdoc/>
    public ValueTask<Result<FirstWorkflowFact>> FirstWorkflow(CancellationToken cancellationToken) => ValueTask.FromResult(FirstWorkflowAnswer);
}
