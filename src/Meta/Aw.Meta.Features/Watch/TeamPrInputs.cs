using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Features.Watch;

/// <summary>What <see cref="TeamPrs"/> needs besides the pull request list: the merge queue, the pull requests lanes sent as ready, and the branches checked out in the team's worktrees.</summary>
/// <param name="repo">Runs git in the team's repo.</param>
/// <param name="values">Holds the pull requests handed to the merge queue.</param>
/// <param name="messages">The inbox, where "ready #N" lands.</param>
/// <param name="paths">Where the gate trees live; the home directory.</param>
/// <param name="files">Reads the configured worktree root.</param>
internal sealed class TeamPrInputs(RepoCommands repo, IKeyValueStore values, IMessageStore messages, MetaPaths paths, IWorktreeFiles files)
{
    /// <summary>The same reads over other git calls (a poll's, bound by its limit and the stop signal).</summary>
    internal TeamPrInputs Using(RepoCommands github) => new(github, values, messages, paths, files);

    /// <summary>Reads the facts.</summary>
    /// <param name="team">The team config (the repo and the worktree roots).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The facts, or the first read that failed.</returns>
    internal async ValueTask<Result<TeamPrFacts>> Read(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var handed = await values.Get(KvKeys.MergeQueued, cancellationToken).ConfigureAwait(false);
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (handed.IsError || inbound.IsError)
        {
            return handed.IsError ? handed.Error : inbound.Error;
        }

        // Every ready message counts, even for a pull request that has since ended: its last PR line still belongs to the team.
        var ready = inbound.Value.Where(m => m.Class is { Kind: MessageKind.Ready, Pr: not null }).Select(m => m.Class.Pr.GetValueOrDefault()).ToHashSet();
        var branches = await CheckedOut(team, cancellationToken).ConfigureAwait(false);
        return branches.IsError ? branches.Error : new TeamPrFacts(MergeQueued.Parse(handed.Value).Keys.ToHashSet(), ready, branches.Value);
    }

    private async ValueTask<Result<IReadOnlySet<string>>> CheckedOut(TeamConfig team, CancellationToken cancellationToken)
    {
        var listed = await repo.Git(team.Repo, "worktree", "list", "--porcelain").ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var repoRoot = await RepoWorktreeRoot.ResolveOrNull(files, team.Repo, paths.Home, cancellationToken).ConfigureAwait(false);
        var roots = team.Worktrees.EffectiveRoots(team.Repo, paths.GatesDirectory, paths.Home, repoRoot);
        return Results.Success(CheckedOutBranches.Under(listed.Value, roots));
    }
}
