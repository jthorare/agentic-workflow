using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a refresh job (owner F1, 2026-10-19; the publish gate, owner decision 2026-10-20): reads the mapped repos' heads, builds the draft, lets the cleanup agent settle its review queue, then asks
/// <see cref="MapPublishDecision"/> whether the draft goes ahead. On publish it promotes through the engine (not the protected command: the owner decided the service promotes, as the job's caller) and records
/// 'context.map.built', 'context.map.promoted' and 'context.map.published'; on wait it stores a hold next to the draft and records 'context.map.held'. Either way it ends with 'context.map.refreshed'.
/// Any step's error fails the job with the later steps not run; a built draft stays. A transient error (see <see cref="MapRefreshRetry"/>: Unavailable, Timeout, RateLimited) runs the whole
/// job again after 30 s, 2 min and 10 min (four attempts), saying so in the job's progress; any other error, and the last attempt's error, fails the job.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="outlooks">The code index's side of the publish gate.</param>
/// <param name="source">Opens the recorder of the map's events under the held lease.</param>
/// <param name="jobs">The running job: its progress takes the step lines.</param>
/// <param name="clock">Dates the build's slot and the start of a wait.</param>
internal sealed class RefreshContextMapService(ContextScopes scopes, IContextEngine engine, IIndexOutlooks outlooks, MapEventSource source, IJobContextAccessor jobs, TimeProvider clock)
    : IService<RefreshContextMapRequest, RefreshContextMapResult>
{
    public async ValueTask<Result<RefreshContextMapResult>> Handle(RefreshContextMapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        for (var attempt = 1; ; attempt++)
        {
            var result = await Attempt(cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess || MapRefreshRetry.WaitBefore(attempt + 1, result.Error) is not { } wait)
            {
                return result;
            }

            jobs.Current?.Progress.Report(MapRefreshRetry.Line(attempt + 1, wait, result.Error));
            await Task.Delay(wait, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<RefreshContextMapResult>> Attempt(CancellationToken cancellationToken)
    {
        var opened = source.Open();
        if (opened.IsError)
        {
            return opened.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var progress = jobs.Current?.Progress;
        var jobScope = scope.Value with { Job = jobs.Current?.Job.Value };   // why: a build waiting behind another names this job
        var heads = await engine.MapHeads(jobScope, cancellationToken).ConfigureAwait(false);
        if (heads.IsError)
        {
            return heads.Error;
        }

        var recorder = opened.Value;
        var built = await Build(recorder, progress, jobScope, cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var settled = await CleanAndGate(recorder, progress, jobScope, built.Value, cancellationToken).ConfigureAwait(false);
        if (settled.IsError)
        {
            return settled.Error;
        }

        var (repo, version, report, held) = settled.Value;
        var result = new RefreshContextMapResult(repo, version,
            heads.Value.ToDictionary(static h => h.Repo.Value, static h => h.Head, StringComparer.Ordinal),
            report.Accepted, report.Removed, report.Dropped, Mode(report.Mode), held);
        var refreshed = await recorder.Refreshed(result, cancellationToken).ConfigureAwait(false);
        return refreshed.IsError ? refreshed.Error : result;
    }

    private async ValueTask<Result<MapBuildReport>> Build(ContextMapEvents recorder, IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken)
    {
        var slot = clock.GetUtcNow();
        progress?.Report("building");
        var built = await engine.BuildMap(progress, scope, cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var recorded = await recorder.Built(built.Value, slot, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : built.Value;
    }

    private async ValueTask<Result<Settled>> CleanAndGate(ContextMapEvents recorder, IProgress<string>? progress, ContextScope scope, MapBuildReport report, CancellationToken cancellationToken)
    {
        // A build that waited behind one whose draft was already promoted returns that current version (#1775): nothing is left to clean, gate or promote.
        if (report.Draft.Status == MapVersionStatus.Current)
        {
            progress?.Report("already current");
            return new Settled(report.Draft.Repo.Value, report.Draft.Number, new CleanupReport(0, 0, 0, CleanupMode.None), Held: false);
        }

        var cleaned = await Clean(progress, scope, report.Draft.Number, cancellationToken).ConfigureAwait(false);
        if (cleaned.IsError)
        {
            return cleaned.Error;
        }

        var gate = await new RefreshGate(engine, outlooks, clock).Run(recorder, scope, report, cancellationToken).ConfigureAwait(false);
        if (gate.IsError)
        {
            return gate.Error;
        }

        if (gate.Value.Publish is not { } publish)
        {
            progress?.Report("held for the code index");
            return new Settled(report.Draft.Repo.Value, report.Draft.Number, cleaned.Value, Held: true);
        }

        progress?.Report("promoting");
        var promoted = await Promote(recorder, scope, publish, gate.Value, cancellationToken).ConfigureAwait(false);
        return promoted.IsError ? promoted.Error : new Settled(promoted.Value.Repo.Value, promoted.Value.Number, cleaned.Value, Held: false);
    }

    private async ValueTask<Result<CleanupReport>> Clean(IProgress<string>? progress, ContextScope scope, int draft, CancellationToken cancellationToken)
    {
        var queue = await engine.ReviewQueue(limit: null, scope, cancellationToken).ConfigureAwait(false);
        if (queue.IsError)
        {
            return queue.Error;
        }

        progress?.Report(string.Create(CultureInfo.InvariantCulture, $"cleaning up: {queue.Value.Undecided} to decide"));
        return await engine.CleanDraft(scope, cancellationToken, draft).ConfigureAwait(false);
    }

    private async ValueTask<Result<MapVersion>> Promote(ContextMapEvents recorder, ContextScope scope, MapPublish publish, RefreshGate.Outcome gate, CancellationToken cancellationToken)
    {
        var promoted = await engine.Promote(scope, cancellationToken, gate.Hold.Version).ConfigureAwait(false);
        if (promoted.IsError)
        {
            return promoted.Error;
        }

        var appended = await recorder.Promoted(promoted.Value, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var published = await recorder.Published(MapGate.Published(promoted.Value.Version, publish, "refresh", gate.Hold, gate.Now), cancellationToken).ConfigureAwait(false);
        return published.IsError ? published.Error : promoted.Value.Version;
    }

    private static string Mode(CleanupMode mode) => mode switch
    {
        CleanupMode.None => "none",
        CleanupMode.Agent => "agent",
        CleanupMode.Partial => "partial",
        CleanupMode.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, message: null),
    };

    private sealed record Settled(string Repo, int Version, CleanupReport Report, bool Held);
}
