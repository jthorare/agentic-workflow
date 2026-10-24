using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Locks;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>
/// Runs the desktop evidence: clears the run directory (so no exit leaves an earlier summary), reads the manifest and the repo's config
/// (a problem with either is a refusal, exit 1), then holds the QA-stack and heavy-job locks while the seeded app is driven through its UI driver
/// and the mockups are rendered and compared. A summary is written on every path once the manifest has been read except a lock that stayed busy
/// (exit 75); a failure of the environment (a fixture command, the app or its driver, the browser) is an errored run (exit 4).
/// </summary>
internal sealed class DesktopRunService(DataRunFiles files, DesktopPlans plans, IMachineLocks locks, DesktopStack stack, DesktopFrames frames, TimeProvider clock)
    : IService<DesktopRunRequest, DesktopRunResponse>
{
    public async ValueTask<Result<DesktopRunResponse>> Handle(DesktopRunRequest request, CancellationToken cancellationToken)
    {
        var prepared = await files.PrepareRunDirectoryAsync(request.RunDirectory, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var bytes = await files.ReadAsync(request.ManifestPath, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        var run = new RunFacts(request, ContentHash.Sha256Hex(bytes.Value), clock.GetUtcNow(), files.ScrubberFor([], []));
        var plan = await plans.PlanAsync(request, bytes.Value, cancellationToken).ConfigureAwait(false);
        return plan.IsError
            ? await StopAsync(run, plan.Error, cancellationToken).ConfigureAwait(false)
            : await RunLockedAsync(run, plan.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<DesktopRunResponse>> RunLockedAsync(RunFacts run, DesktopPlan plan, CancellationToken cancellationToken)
    {
        var lease = await StackLocks.AcquireAsync(locks, run.Request.LockTimeout, cancellationToken).ConfigureAwait(false);
        if (lease.IsError)
        {
            // A lock that stayed busy is the caller's to retry and records nothing; any other failure is this run's error.
            return lease.Error.Kind == ErrorKind.Timeout ? lease.Error : await StopAsync(run, lease.Error, cancellationToken).ConfigureAwait(false);
        }

        await using (lease.Value.ConfigureAwait(false))
        {
            var driven = await stack.RunAsync(plan, cancellationToken).ConfigureAwait(false);
            if (driven.IsError)
            {
                return await StopAsync(run, driven.Error, cancellationToken).ConfigureAwait(false);
            }

            run = run with { Notes = driven.Value.Notes };

            var compared = await frames.CompareAsync(plan, driven.Value.Problems, cancellationToken).ConfigureAwait(false);
            return compared.IsError
                ? await StopAsync(run, compared.Error, cancellationToken).ConfigureAwait(false)
                : await WriteAsync(run, Finished(run, compared.Value), cancellationToken).ConfigureAwait(false);
        }
    }

    // An error of the input stops the run as a refusal (exit 1); a failure of the environment (unavailable) as an errored run (exit 4). No screen was judged either way.
    private ValueTask<Result<DesktopRunResponse>> StopAsync(RunFacts run, Error error, CancellationToken cancellationToken)
    {
        var coded = new CodedMessage(error.Code, error.Message);
        var summary = error.Kind == ErrorKind.Unavailable
            ? Compose(run, new Verdict(DesktopOutcome.Errored, "errored: " + error.Message, [], Failure: coded))
            : Compose(run, new Verdict(DesktopOutcome.Refused, Number.Text($"refused: {error.Code} {Number.ShortPaths(error.Message)}"), [], Refusal: coded));
        return WriteAsync(run, summary, cancellationToken);
    }

    private async ValueTask<Result<DesktopRunResponse>> WriteAsync(RunFacts run, DesktopSummary summary, CancellationToken cancellationToken)
    {
        var scrubbed = summary with { Refusal = DataRunFiles.Messaged(summary.Refusal, run.Scrubber), Error = DataRunFiles.Messaged(summary.Error, run.Scrubber) };
        var written = await files.WriteDocumentAsync(run.Request.RunDirectory, scrubbed, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        var report = await frames.WriteReportAsync(run.Request.RunDirectory, written.Value.Summary, cancellationToken).ConfigureAwait(false);
        return report.IsError ? report.Error : new DesktopRunResponse(written.Value.Summary, written.Value.Document);
    }

    private DesktopSummary Finished(RunFacts run, IReadOnlyList<DesktopScreenResult> screens)
    {
        var failed = screens.Where(static s => !s.Passed).ToList();
        var headline = failed.Count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"passed: {screens.Count} {(screens.Count == 1 ? "screen" : "screens")} within their limit")
            : string.Create(CultureInfo.InvariantCulture, $"failed: {failed[0].Name} at {failed[0].Size} {Describe(failed[0])} ({failed.Count} of {screens.Count} over)");
        return Compose(run, new Verdict(failed.Count == 0 ? DesktopOutcome.Passed : DesktopOutcome.Failed, headline, screens));
    }

    private static string Describe(DesktopScreenResult screen) =>
        screen.Problem ?? string.Create(CultureInfo.InvariantCulture, $"differs {screen.DiffPercent:0.##}% (limit {screen.Threshold:0.##}%)");

    private DesktopSummary Compose(RunFacts run, Verdict verdict) =>
        new(
            DesktopSummary.SchemaName, DesktopSummary.KindName, RunId.From(RunPath.NameOf(run.Request.RunDirectory)), run.Started, clock.GetUtcNow(), run.Request.AppBuild,
            run.Sha256, verdict.Outcome, RecordedText.Of(run.Scrubber, verdict.Headline, RecordedText.HeadlineLimit), verdict.Screens, verdict.Refusal, verdict.Failure, run.Notes);

    private sealed record Verdict(DesktopOutcome Outcome, string Headline, IReadOnlyList<DesktopScreenResult> Screens, CodedMessage? Refusal = null, CodedMessage? Failure = null);

    private sealed record RunFacts(DesktopRunRequest Request, string Sha256, DateTimeOffset Started, ITextScrubber Scrubber)
    {
        internal IReadOnlyList<string> Notes { get; init; } = [];
    }
}
