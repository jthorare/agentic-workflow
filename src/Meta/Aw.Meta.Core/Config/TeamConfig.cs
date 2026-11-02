using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>
/// The team the meta session coordinates: everything team-specific lives here, nothing is built into the code.
/// Unknown fields in the file are ignored; every field but the first three is optional.
/// </summary>
public sealed record TeamConfig
{
    /// <summary>Creates a config.</summary>
    /// <param name="team">The team name; session names are "team-lane".</param>
    /// <param name="repo">The repository directory the gate and merge commands work in.</param>
    /// <param name="lanes">The lane names, the meta lane included.</param>
    /// <param name="metaLane">The meta lane's name.</param>
    /// <param name="log">The running log file; null means the default under the state directory.</param>
    /// <param name="prompts">A directory of per-lane kickoff prompts, read only by the launcher script.</param>
    /// <param name="gate">The gate scopes; none when absent.</param>
    /// <param name="mergeOrder">Merge-order rules; none when absent.</param>
    /// <param name="knownFlakes">Names of tests known to be flaky; none when absent.</param>
    /// <param name="worktrees">The worktree sweep's settings; the defaults when absent.</param>
    /// <param name="resources">The memory and disk thresholds; the defaults when absent.</param>
    /// <param name="builders">How many builders a lane may run; the defaults when absent.</param>
    /// <param name="heavy">The heavy-slot reservation; the defaults when absent.</param>
    /// <param name="context">The lane context-window threshold for recycling; the defaults when absent.</param>
    /// <param name="usage">The usage-limit monitor's margin, probe interval and auto-resume; the defaults when absent.</param>
    /// <param name="agent">The catalog agent every lane runs, including the meta lane. Null leaves the choice to launcher.json.</param>
    [JsonConstructor]
    public TeamConfig(
        string team,
        string repo,
        IReadOnlyList<string> lanes,
        string metaLane = "meta",
        string? log = null,
        string? prompts = null,
        GateConfig? gate = null,
        IReadOnlyList<MergeOrderRule>? mergeOrder = null,
        IReadOnlyList<string>? knownFlakes = null,
        WorktreeConfig? worktrees = null,
        ResourcesConfig? resources = null,
        BuildersConfig? builders = null,
        HeavyConfig? heavy = null,
        ContextConfig? context = null,
        UsageConfig? usage = null,
        string? agent = null)
    {
        Team = team;
        Repo = repo;
        Lanes = lanes;
        MetaLane = metaLane;
        Log = log;
        Prompts = prompts;
        Gate = gate ?? GateConfig.Empty;
        MergeOrder = mergeOrder ?? [];
        KnownFlakes = knownFlakes ?? [];
        Worktrees = worktrees ?? new WorktreeConfig();
        Resources = resources ?? new ResourcesConfig();
        Builders = builders ?? new BuildersConfig();
        Heavy = heavy ?? new HeavyConfig();
        Context = context ?? new ContextConfig();
        Usage = usage ?? new UsageConfig();
        Agent = agent;
    }

    /// <summary>The team name; session names are "team-lane".</summary>
    public string Team { get; init; }

    /// <summary>The repository directory the gate and merge commands work in.</summary>
    public string Repo { get; init; }

    /// <summary>The lane names, the meta lane included.</summary>
    public IReadOnlyList<string> Lanes { get; init; }

    /// <summary>The meta lane's name.</summary>
    public string MetaLane { get; init; }

    /// <summary>The running log file; null means the default under the state directory.</summary>
    public string? Log { get; init; }

    /// <summary>A directory of per-lane kickoff prompts, read only by the launcher script.</summary>
    public string? Prompts { get; init; }

    /// <summary>The gate scopes.</summary>
    public GateConfig Gate { get; init; }

    /// <summary>Merge-order rules.</summary>
    public IReadOnlyList<MergeOrderRule> MergeOrder { get; init; }

    /// <summary>Names of tests known to be flaky.</summary>
    public IReadOnlyList<string> KnownFlakes { get; init; }

    /// <summary>The worktree sweep's settings.</summary>
    public WorktreeConfig Worktrees { get; init; }

    /// <summary>The memory and disk thresholds.</summary>
    public ResourcesConfig Resources { get; init; }

    /// <summary>How many builders a lane may run.</summary>
    public BuildersConfig Builders { get; init; }

    /// <summary>The heavy-slot reservation.</summary>
    public HeavyConfig Heavy { get; init; }

    /// <summary>The lane context-window threshold for recycling.</summary>
    public ContextConfig Context { get; init; }

    /// <summary>When the usage-limit monitor calls a limit over and whether it resumes idle lanes.</summary>
    public UsageConfig Usage { get; init; }

    /// <summary>The catalog agent every lane runs, including the meta lane. Null leaves the choice to launcher.json.</summary>
    public string? Agent { get; init; }
}
