using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Obligations;
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
/// <param name="gates">Stores the merge.</param>
/// <param name="log">The running log.</param>
/// <param name="reservation">Computes the heavy-slot reservation again once the merge has left the queue.</param>
/// <param name="clock">Dates the merge.</param>
/// <param name="board">Records main's new sha and the merged pull request.</param>
internal sealed class MergeRunService(MergeReadiness readiness, RepoCommands repo, IGateStore gates, IRunningLog log, HeavyReservation reservation, TimeProvider clock, IBoardStore board)
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
                ? await Record(request, assessed.Value, pull, await repo.MergeCommit(repoDirectory, request.Pr).ConfigureAwait(false), cancellationToken).ConfigureAwait(false)
                : Refused(request, assessed.Value);
        }

        var number = request.Pr.ToString(CultureInfo.InvariantCulture);
        var merged = await repo.Gh(repoDirectory, "pr", "merge", number, "--merge").ConfigureAwait(false);
        return merged.IsError
            ? merged.Error
            : await Record(request, assessed.Value, pull, await MainSha(repoDirectory, request.Pr).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<Result<MergeRunResponse>> Record(MergeRunRequest request, MergeAssessment assessed, PrDetails pull, Result<string> sha, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var record = new MergeRecord(request.Pr, sha.IsSuccess ? sha.Value : MergeRecord.Unknown, now);
        var recorded = await gates.RecordMerge(record, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        // Advisory, like the reservation below: the merge happened either way, and a sha that can't be read can't be shown to lanes.
        if (record.IsKnown)
        {
            _ = await board.RecordMain(record.Sha, [request.Pr], now, cancellationToken).ConfigureAwait(false);
        }

        var number = request.Pr.ToString(CultureInfo.InvariantCulture);
        var logged = await log.Append(LogLine.Event(LogKind.Merge, now, $"#{number} {pull.Title} merged; main is now {(record.IsKnown ? record.ShortSha : "unknown")}"), cancellationToken).ConfigureAwait(false);
        var announce = record.IsKnown
            ? $"main moved to {record.ShortSha}: #{number} {pull.Title} merged. Merge main into your branches."
            : $"main moved (#{number} {pull.Title} merged). Merge main into your branches.";

        // Advisory, and before the log error returns: the merge left the queue either way, and a reservation that can't be kept must not turn it into an error.
        _ = await reservation.Apply(assessed.Team, cancellationToken).ConfigureAwait(false);
        if (logged.IsError)
        {
            return logged.Error;
        }

        return new MergeRunResponse(request.Pr, Merged: true, assessed.Checks, record.IsKnown ? record.Sha : null, announce);
    }
}
