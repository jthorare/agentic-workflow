using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.MergeRun;

/// <summary>
/// Merges a pull request once every check holds: "gh pr merge --merge", the merge recorded with main's new sha (and put on
/// the board lanes read through board-line), a running-log line, and the "main moved" text for the log and the owner. The merge is recorded as soon as gh
/// succeeds: main's sha comes from a fetch, else from GitHub's merge commit, else stays unknown. A pull request that is
/// already merged on GitHub with no merge recorded (an earlier run died after gh) is recorded now instead.
/// </summary>
/// <param name="readiness">Checks the pull request.</param>
/// <param name="repo">The git and gh calls.</param>
/// <param name="gates">Finds the merge of a pull request that was merged already.</param>
/// <param name="recorder">Records the merge, or the hand-over to GitHub's merge queue.</param>
internal sealed class MergeRunService(MergeReadiness readiness, RepoCommands repo, IGateStore gates, MergeRecorder recorder)
    : IService<MergeRunRequest, MergeRunResponse>
{
    public async ValueTask<Result<MergeRunResponse>> Handle(MergeRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var assessed = await readiness.Assess(request.Pr, cancellationToken).ConfigureAwait(false);
        if (assessed.IsError)
        {
            return assessed.Error;
        }

        var repoDirectory = assessed.Value.Team.Repo;
        if (assessed.Value.Pr is not { } pull)
        {
            return Refused(request, assessed.Value);
        }

        if (!assessed.Value.Ready)
        {
            return await Unrecorded(pull, request.Pr, cancellationToken).ConfigureAwait(false)
                ? await recorder.Record(request, assessed.Value, pull, await repo.MergeCommit(repoDirectory, request.Pr).ConfigureAwait(false), cancellationToken).ConfigureAwait(false)
                : Refused(request, assessed.Value);
        }

        return await MergeReady(request, assessed.Value, pull, cancellationToken).ConfigureAwait(false);
    }

    // The queue is read first: a failed read merges nothing. With a merge queue gh only enqueues the pull request, so the merge is not recorded here; the watch records it when it lands.
    private async ValueTask<Result<MergeRunResponse>> MergeReady(MergeRunRequest request, MergeAssessment assessed, PrDetails pull, CancellationToken cancellationToken)
    {
        var repoDirectory = assessed.Team.Repo;
        var view = await repo.QueueView(repoDirectory, request.Pr).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var merged = await repo.Gh(repoDirectory, "pr", "merge", request.Pr.ToString(CultureInfo.InvariantCulture), "--merge").ConfigureAwait(false);
        if (merged.IsError)
        {
            return merged.Error;
        }

        return view.Value.Enabled
            ? await recorder.Enqueue(request.Pr, assessed, pull, cancellationToken).ConfigureAwait(false)
            : await recorder.Record(request, assessed, pull, await MainSha(repoDirectory, request.Pr).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> Unrecorded(PrDetails pull, int pr, CancellationToken cancellationToken) =>
        pull.State == PrState.Merged && await gates.FindMerge(pr, cancellationToken).ConfigureAwait(false) is { IsSuccess: true, Value: null };

    private static MergeRunResponse Refused(MergeRunRequest request, MergeAssessment assessed) =>
        new(request.Pr, Merged: false, assessed.Checks, Sha: null, Announce: null);

    // Main's tip after a fetch; failing that, the commit GitHub made; failing that, unknown. The merge happened either way.
    private async ValueTask<Result<string>> MainSha(string repoDirectory, int pr)
    {
        var fetched = await repo.Fetch(repoDirectory, pr: null).ConfigureAwait(false);
        var sha = fetched.IsError ? Results.Failure<string>(fetched.Error) : await repo.Sha(repoDirectory, "origin/main").ConfigureAwait(false);
        return sha.IsSuccess ? sha : await repo.MergeCommit(repoDirectory, pr).ConfigureAwait(false);
    }
}
