using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;
using IndexRepoStatus = Aw.CodeIndex.Core.Facade.RepoStatus;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The 'code-index-embed' background service (code index spec §6.3): for each repo of the approved profile whose index has its embeddings layer pending, it starts an 'EmbedCodeIndex' job of key 'embed:&lt;repo&gt;'
/// as itself, through the command chain, and keeps doing so until the layer is ok, so a repo is fully embedded without any HEAD move (a move leaves only the changed files' symbols pending). The job takes no heavy-job slot.
/// Each repo is decided on its own by <see cref="EmbedDecision"/>. A repo whose last jobs failed is named in the summary, and the run stays ok: a model that is not there is a layer state, not a needs-you item.
/// A tick reads the index status and the jobs once.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="index">The code index: its status.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="start">The EmbedCodeIndex command chain.</param>
/// <param name="clock">Dates the backoff.</param>
/// <param name="logger">Logs a 409 that is not "the embed is already running".</param>
internal sealed partial class CodeIndexEmbedService(CodeIndexJobScopes scopes, ICodeIndex index, IJobViews jobs, IService<EmbedCodeIndexRequest, JobStartedResponse> start, TimeProvider clock, ILogger<CodeIndexEmbedService> logger) : IScheduledService
{
    private const int RecentRuns = 8;

    internal static ServiceName ServiceId { get; } = ServiceName.From("code-index-embed");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Embeds the code-index chunks that are not embedded yet.";

    /// <summary>Runs without a profile entry, every minute: a tick is one status read and one jobs read.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Repos.Count == 0)
        {
            // no approved profile, or no repo, is nothing to embed, not a failed run (a failed run would land in needs-you every tick)
            return scope.IsError && scope.Error.Code != SindriErrors.ProfileNotApproved.Code ? scope.Error : ScheduledResult.Quiet;
        }

        var status = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        var embeds = await jobs.List(state: null, IndexJobs.EmbedKind, cancellationToken).ConfigureAwait(false);
        if (status.IsError || embeds.IsError)
        {
            return status.IsError ? status.Error : embeds.Error;
        }

        var swept = await Sweep(status.Value, embeds.Value, cancellationToken).ConfigureAwait(false);
        return swept.IsError ? swept.Error : new ScheduledResult(swept.Value.Started.Count == 0 && swept.Value.Failing.Count == 0, Summary(swept.Value.Started, swept.Value.Failing));
    }

    private async ValueTask<Result<Sweeps>> Sweep(StatusAnswer status, IReadOnlyList<JobRow> embeds, CancellationToken cancellationToken)
    {
        var sweeps = new Sweeps();
        foreach (var repo in status.Repos)
        {
            var step = await Step(repo, embeds, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.Started)
            {
                sweeps.Started.Add(repo.Repo.Value);
            }

            if (step.Value.Failing)
            {
                sweeps.Failing.Add(repo.Repo.Value);
            }
        }

        return sweeps;
    }

    private async ValueTask<Result<RepoStep>> Step(IndexRepoStatus repo, IReadOnlyList<JobRow> embeds, CancellationToken cancellationToken)
    {
        var name = repo.Repo.Value;
        var pending = repo.State == IndexState.Ok && repo.Layers.Any(static l => l.Layer == LayerName.Embeddings && l.State == LayerState.Pending);
        var mine = embeds.Where(j => IndexJobs.IsEmbedOf(j, name)).ToList();
        var attempts = Attempts(mine);
        var action = EmbedDecision.Decide(pending, mine.Exists(static j => j.State == JobState.Running), attempts, clock.GetUtcNow());
        var failing = pending && attempts is [{ Kind: EmbedAttemptKind.Failed }, ..];
        if (action != MainMoveAction.Start)
        {
            return new RepoStep(Failing: failing);
        }

        var response = await start.Handle(new EmbedCodeIndexRequest(name), cancellationToken).ConfigureAwait(false);
        if (response.IsError)
        {
            return response.Error.Code != SindriErrors.JobConflict.Code ? response.Error : new RepoStep(Failing: failing, Started: Conflict(response.Error));
        }

        return new RepoStep(Started: true, Failing: failing);
    }

    // The ended jobs, newest first, each read as an attempt: a pending job is "no progress" when it ended with no more vectors than the job before it.
    private static List<EmbedAttempt> Attempts(List<JobRow> mine)
    {
        var finished = mine.Where(static j => j.State is JobState.Failed or JobState.Succeeded && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).Take(RecentRuns + 1).ToList();
        var attempts = new List<EmbedAttempt>();
        for (var i = 0; i < Math.Min(finished.Count, RecentRuns); i++)
        {
            attempts.Add(new EmbedAttempt(Kind(finished[i], finished.Skip(i + 1).FirstOrDefault(static j => j.State == JobState.Succeeded && IndexJobs.OutcomeOf(j) != EmbedCodeIndexResult.Later)), finished[i].EndedAt ?? default));
        }

        return attempts;
    }

    private static EmbedAttemptKind Kind(JobRow job, JobRow? before)
    {
        if (job.State == JobState.Failed)
        {
            return EmbedAttemptKind.Failed;
        }

        var outcome = IndexJobs.OutcomeOf(job);
        if (outcome == EmbedCodeIndexResult.Later)
        {
            return EmbedAttemptKind.TryLater;
        }

        return outcome == EmbedCodeIndexResult.Pending && before is not null && IndexJobs.EmbeddedOf(job) <= IndexJobs.EmbeddedOf(before)
            ? EmbedAttemptKind.NoProgress
            : EmbedAttemptKind.Progress;
    }

    // 409 with 'runningJob' is the same embed running: nothing to say. Any other 409 is logged, and not retried this tick.
    private bool Conflict(Error error)
    {
        if (error.Extensions is null || !error.Extensions.Any(static e => e.Key == "runningJob"))
        {
            LogConflict(logger, error.Code, error.Message);
        }

        return false;
    }

    private static string? Summary(List<string> started, List<string> failing)
    {
        var notes = new List<string>();
        if (started.Count > 0)
        {
            notes.Add("ok: embedding started for " + string.Join(", ", started));
        }

        if (failing.Count > 0)
        {
            notes.Add("the last embed of " + string.Join(", ", failing) + " failed; the next try backs off");
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "code-index-embed: the embed was refused with {Code} and is not retried this tick: {Detail}")]
    private static partial void LogConflict(ILogger logger, string code, string? detail);

    private readonly record struct RepoStep(bool Started = false, bool Failing = false);

    private sealed record Sweeps
    {
        internal List<string> Started { get; } = [];

        internal List<string> Failing { get; } = [];
    }
}
