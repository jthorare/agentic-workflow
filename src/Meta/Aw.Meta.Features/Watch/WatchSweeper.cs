using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// The watch's worktree trigger: when a poll sees a pull request become merged or closed, sweeps that pull request's worktrees. A sweep
/// that could not run (GitHub or git unreachable) leaves the pull requests pending, and the next poll tries them again. Nothing here looks at age.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="sweeper">Sweeps the worktrees.</param>
/// <param name="values">Keeps the pending pull requests.</param>
internal sealed class WatchSweeper(IMetaConfig config, WorktreeSweeper sweeper, IKeyValueStore values)
{
    /// <summary>The same sweeping over a poll's gh and git calls, bound by its limit and the stop signal.</summary>
    internal WatchSweeper Using(Core.Coordination.RepoCommands github) => new(config, sweeper.Using(github), values);

    /// <summary>Sweeps the pull requests that just ended, and any still pending.</summary>
    /// <param name="ended">The pull requests this poll saw end for the first time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The summary lines to emit (none when no worktree belongs to those pull requests or the sweep could not run yet).</returns>
    internal async ValueTask<IReadOnlyList<string>> Sweep(IReadOnlyList<int> ended, CancellationToken cancellationToken)
    {
        var stored = await values.Get(KvKeys.WorktreesPending, cancellationToken).ConfigureAwait(false);
        var pending = EndedPrs.Parse(stored.IsSuccess ? stored.Value : null).Union(ended).Order().ToList();
        if (pending.Count == 0)
        {
            return [];
        }

        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var line = team.IsError ? Results.Failure<string>(team.Error) : await sweeper.Triggered(team.Value, pending, "watch", cancellationToken).ConfigureAwait(false);
        _ = await values.Set(KvKeys.WorktreesPending, line.IsError ? string.Join(',', pending.Select(static n => n.ToString(CultureInfo.InvariantCulture))) : string.Empty, cancellationToken).ConfigureAwait(false);
        return line.IsSuccess && line.Value.Length > 0 ? [line.Value] : [];
    }
}
