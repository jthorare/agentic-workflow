using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.MergeRun;

/// <summary>
/// Keeps what merge-run did. A merge is recorded, put on the board and logged, with the "main moved" text. A pull request GitHub's
/// merge queue accepted is only remembered under merge.queued and logged: no merge is recorded, no board line written and
/// nothing announced, and the watch settles it when GitHub merges it or drops it.
/// </summary>
/// <param name="repo">Reads the queue position.</param>
/// <param name="values">Holds the queued pull requests.</param>
/// <param name="gates">Stores the merge.</param>
/// <param name="log">The running log.</param>
/// <param name="reservation">Computes the heavy-slot reservation again once the pull request has left the queue.</param>
/// <param name="clock">Dates the entry.</param>
/// <param name="board">Records main's new sha and the merged pull request.</param>
internal sealed class MergeRecorder(RepoCommands repo, IKeyValueStore values, IGateStore gates, IRunningLog log, HeavyReservation reservation, TimeProvider clock, IBoardStore board)
{
    internal async ValueTask<Result<MergeRunResponse>> Enqueue(int pr, MergeAssessment assessed, PrDetails pull, CancellationToken cancellationToken)
    {
        // Advisory: the position is for the text only, so a failed read leaves it out.
        var view = await repo.QueueView(assessed.Team.Repo, pr).ConfigureAwait(false);
        var position = view.IsSuccess ? view.Value.Position : null;
        var stored = await values.Get(KvKeys.MergeQueued, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var now = clock.GetUtcNow();
        var kept = await values.Set(KvKeys.MergeQueued, MergeQueued.With(stored.Value, new QueuedMerge(pr, now, pull.Head)), cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
        }

        var place = position is { } number ? string.Create(CultureInfo.InvariantCulture, $" (position {number})") : string.Empty;
        var logged = await log.Append(LogLine.Event(LogKind.Note, now, string.Create(CultureInfo.InvariantCulture, $"#{pr} {pull.Title} queued in GitHub's merge queue{place}")), cancellationToken).ConfigureAwait(false);

        // Advisory, like the merge path's: a reservation that can't be kept must not turn an enqueued pull request into an error.
        _ = await reservation.Apply(assessed.Team, cancellationToken).ConfigureAwait(false);
        return logged.IsError
            ? logged.Error
            : new MergeRunResponse(pr, Merged: false, assessed.Checks, Sha: null, Announce: null, Queued: true, Position: position);
    }

    internal async ValueTask<Result<MergeRunResponse>> Record(MergeRunRequest request, MergeAssessment assessed, PrDetails pull, Result<string> sha, CancellationToken cancellationToken)
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
