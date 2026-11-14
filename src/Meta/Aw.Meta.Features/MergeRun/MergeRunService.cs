using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
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
/// <param name="events">Writes merge.recorded once a merge is recorded; null writes none.</param>
internal sealed class MergeRunService(MergeReadiness readiness, RepoCommands repo, IGateStore gates, MergeRecorder recorder, MergeEventWriter? events = null)
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
                ? await Recorded(await recorder.Record(request, assessed.Value, pull, await repo.MergeCommit(repoDirectory, request.Pr).ConfigureAwait(false), cancellationToken).ConfigureAwait(false), assessed.Value, cancellationToken).ConfigureAwait(false)
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

        // Already in GitHub's queue (an earlier run enqueued it): merging again would only fail or re-enqueue, so refresh the entry.
        if (view.Value is { Enabled: true, InQueue: true })
        {
            return await recorder.Enqueue(request.Pr, assessed, pull, cancellationToken).ConfigureAwait(false);
        }

        var merged = await Merge(repoDirectory, request.Pr, view.Value.Stacked).ConfigureAwait(false);
        if (merged.IsError)
        {
            return merged.Error;
        }

        if (view.Value.Enabled)
        {
            return await recorder.Enqueue(request.Pr, assessed, pull, cancellationToken).ConfigureAwait(false);
        }

        // Unlike "gh pr merge", an exit 0 of "gh stack merge" is not proof the pull request merged (it may have been accepted for a later merge): read it again before recording.
        if (view.Value.Stacked && await StillOpen(repoDirectory, request.Pr).ConfigureAwait(false) is { } open)
        {
            return open;
        }

        return await Recorded(await recorder.Record(request, assessed, pull, await MainSha(repoDirectory, request.Pr).ConfigureAwait(false), cancellationToken).ConfigureAwait(false), assessed, cancellationToken).ConfigureAwait(false);
    }

    // Null when the pull request reads MERGED; otherwise the refusal (AW-META-012), which records nothing.
    private async ValueTask<Error?> StillOpen(string repoDirectory, int pr)
    {
        var now = await repo.View(repoDirectory, pr, "state").ConfigureAwait(false);
        return now.IsError
            ? now.Error
            : now.Value.State == PrState.Merged
                ? null
                : MetaErrors.CommandFailed.With($"gh stack merge #{pr.ToString(CultureInfo.InvariantCulture)} exited 0 but the pull request is still open; nothing is recorded, run merge-run again when it has merged");
    }

    // GitHub refuses a stacked pull request on the plain merge call: "gh stack merge" merges the stack up to it (or enqueues it behind a merge queue). Without the extension a stacked pull request is refused before anything runs.
    private async ValueTask<Result<string>> Merge(string repoDirectory, int pr, bool stacked)
    {
        var number = pr.ToString(CultureInfo.InvariantCulture);
        if (!stacked)
        {
            return await repo.Gh(repoDirectory, "pr", "merge", number, "--merge").ConfigureAwait(false);
        }

        return await StackExtensionMissing(repoDirectory).ConfigureAwait(false) is { } missing
            ? missing
            : await repo.Gh(repoDirectory, "stack", "merge", number, "--yes").ConfigureAwait(false);
    }

    // Null when the gh stack extension is installed; otherwise the refusal that names the install command (a failed listing is gh's own error).
    private async ValueTask<Error?> StackExtensionMissing(string repoDirectory)
    {
        var list = await repo.Gh(repoDirectory, "extension", "list").ConfigureAwait(false);
        if (list.IsError)
        {
            return list.Error;
        }

        return GhExtensions.HasStack(list.Value)
            ? null
            : MetaErrors.StackExtensionMissing.With($"the pull request is part of a stack and gh stack merges it, but the extension is not installed; run: {GhExtensions.StackInstall}");
    }

    // A recorded merge is an event; a failed write of it never changes the answer.
    private async ValueTask<Result<MergeRunResponse>> Recorded(Result<MergeRunResponse> result, MergeAssessment assessed, CancellationToken cancellationToken)
    {
        if (events is not null && result is { IsSuccess: true, Value.Merged: true })
        {
            await events.Write(result.Value, assessed.Team, cancellationToken).ConfigureAwait(false);
        }

        return result;
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
