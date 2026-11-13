using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'LaunchAgentNode' does (PR 7): reads the node, interpolates its config, resolves the repo and the agent from the profile, asks the launcher for the unattended session of
/// <see cref="RunAgentLaunches.RequestFor"/> in a fresh worktree on the node's branch, records the outcome, and reads the worktree's HEAD as the node's start. The launch id is the node run's,
/// so a retry or a replay starts no second session.
/// </summary>
/// <param name="reading">Reads the run, the node's config and its prompt with every reference fenced as untrusted (#2447).</param>
/// <param name="workflows">Where the workflow's declared resources are read from.</param>
/// <param name="approved">The live profile: the repos and the default agent.</param>
/// <param name="starting">Asks the launcher for the session and records the outcome.</param>
/// <param name="provisioner">Makes the workflow's declared resources exist before the session starts; null on a build with none.</param>
internal sealed class AgentNodeLaunching(AgentNodeReading reading, IWorkflowStore workflows, IApprovedProfile approved, AgentLaunchStart starting, IResourceProvisioner? provisioner = null)
{
    private readonly GraphResourceEnsurer _resources = new(provisioner, workflows);

    /// <summary>Launches the node run's session.</summary>
    /// <param name="node">The node run.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The worktree and start commit, or the failure, or that the session cap made it wait.</returns>
    public async Task<AgentNodeLaunched> Launch(AgentNodeRef node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        var read = await reading.Read(node, cancellationToken).ConfigureAwait(false);
        var row = read.Run;
        var config = read.Config;
        var input = StoredJson.Fit(config.ToJsonString());   // why: a big interpolated prompt would go into Temporal history raw and pass its 2 MB limit; the row keeps the fitted text (#3486)
        if (StoredJson.ReadFailure(read.Scope, read.CutNode) is { } unreadable)
        {
            return Failed(read.CutNode is null ? input : new Fitted("{}", Cut: false), unreadable);   // why: a cut or blocked output the references read, or the list a loop needs
        }

        var parsed = RunAgentConfig.Parse(config);
        if (parsed.IsError)
        {
            return Failed(input, parsed.Error);
        }

        var task = read.Prompt is { } fenced ? parsed.Value with { Prompt = fenced } : parsed.Value;

        var placed = await Place(parsed.Value, row, cancellationToken).ConfigureAwait(false);
        if (placed.IsError)
        {
            return Failed(input, placed.Error);
        }

        var address = NodeRunAddress.Of(node.Run, node.Node, node.ItemIndex);
        var token = RunAgentWire.TokenOf(read.Spec, node.Node, node.ItemIndex) ?? throw new ApplicationFailureException($"the spec has no node '{node.Node.Value}'", nonRetryable: true);
        var brief = RunAgentBrief.Render(address, token, task);
        var groups = RunAgentLaunches.GroupsFor(parsed.Value, static _ => []);   // why: a connection's host group comes with the connection presets (PR 10); until then only the repo's 'github' applies
        var request = RunAgentLaunches.ReadingOnly(RunAgentLaunches.RequestFor(address, placed.Value.Target, parsed.Value, brief, groups), placed.Value.ReadOnlyPaths);

        // why: idempotent; the workflow's database and disk folders and its declared sandbox size reach the session's plan (#2532), made after the ended check so a retry never fails on them
        var launched = await starting.Start(request, ct => _resources.Ensure(row.WorkflowId, read.Spec, ct), cancellationToken).ConfigureAwait(false);
        return new AgentNodeLaunched(launched, input.Text, parsed.Value.Timeout, InputCut: input.Cut);
    }

    // The repo checkout, the agent and the read-only checkouts from the live profile; the run's item, or one made from the run's id when it has none.
    private async ValueTask<Result<Placed>> Place(RunAgentConfig config, WorkflowRun row, CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var profile = latest.Value?.Profile;
        if (RepoPath(profile, config.Repo) is not { } path)
        {
            return SindriErrors.LaunchRepoMissing.With($"the node's repo '{config.Repo}' is not in the profile's repos (fix: add it)");
        }

        var agent = AgentOf(profile, config);
        var readOnly = ReadOnlyPaths(profile, config);
        if (agent.IsError || readOnly.IsError)
        {
            return agent.IsError ? agent.Error : readOnly.Error;
        }

        return new Placed(new AgentStepTarget(row.ItemId ?? ItemOf(row.Id.Value), agent.Value, path, RepoName.TryFrom(config.Repo, out var repo) ? repo : null), readOnly.Value);
    }

    private static Result<AgentId> AgentOf(SindriProfile? profile, RunAgentConfig config)
    {
        if (config.Agent is { } named && AgentId.TryFrom(named, out var chosen))
        {
            return chosen;
        }

        return profile?.Workflow?.Agent?.Target is { } target
            ? target
            : SindriErrors.LaunchAgentMissing.With("the node names no agent and the profile has no workflow.agent.target (fix: set one)");
    }

    private static Result<IReadOnlyList<string>> ReadOnlyPaths(SindriProfile? profile, RunAgentConfig config)
    {
        List<string> paths = [];
        foreach (var name in config.ReadOnlyRepos)
        {
            if (RepoPath(profile, name) is not { } readable)
            {
                return SindriErrors.LaunchRepoMissing.With($"the node's read-only repo '{name}' is not in the profile's repos (fix: add it)");
            }

            paths.Add(readable);
        }

        return paths;
    }

    private static string? RepoPath(SindriProfile? profile, string name) =>
        profile?.Repos?.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal))?.Path;

    // why: a run no item started has no item id, and a launch needs one; the id is made from the run's, so a replay makes the same one
    private static ItemId ItemOf(string run) =>
        ItemId.From("ITM-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("graph-run:" + run)))[..32]);

    private static AgentNodeLaunched Failed(Fitted input, Error error) =>
        new(new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, error.Code, error.Message)), input.Text, TimeSpan.Zero, InputCut: input.Cut);

    private sealed record Placed(AgentStepTarget Target, IReadOnlyList<string> ReadOnlyPaths);
}
