using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Locks;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>
/// Runs an API script: clears the run directory (so no exit leaves an earlier summary), reads and checks the script, vets the host,
/// headers and goldens (refusals take no lock), then holds the QA-stack and heavy-job locks while it runs the requests, and writes a
/// scrubbed summary.json on every path once the script has been read except a lock that stayed busy (exit 75). An error of the
/// input is a refusal (exit 1); a failure of the environment's I/O (an unreadable golden, a lock directory that cannot be made) is
/// an errored run (exit 4).
/// </summary>
internal sealed class ApiRunService(DataRunFiles files, ApiVetting vetting, RequestRunner requests, CallRunner calls, IMachineLocks locks, TimeProvider clock, IIdGenerator ids)
    : IService<ApiRunRequest, ApiRunResponse>
{
    public async ValueTask<Result<ApiRunResponse>> Handle(ApiRunRequest request, CancellationToken cancellationToken)
    {
        var prepared = await files.PrepareRunDirectoryAsync(request.RunDirectory, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var bytes = await files.ReadAsync(request.ScriptPath, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        var run = new RunFacts(request, ContentHash.Sha256Hex(bytes.Value), RunInstanceId.From(ids.NewId().ToString("D")), clock.GetUtcNow(), files.ScrubberFor([], []));
        var script = files.Parse(request.ScriptPath, bytes.Value);
        if (script.IsError)
        {
            return await StopAsync(run, new Stopped(Script: null, Host: null, script.Error), cancellationToken).ConfigureAwait(false);
        }

        var vetted = await vetting.VetAsync(request, script.Value, cancellationToken).ConfigureAwait(false);
        if (vetted.IsError)
        {
            return await StopAsync(run, new Stopped(script.Value, Host: null, vetted.Error), cancellationToken).ConfigureAwait(false);
        }

        return await RunLockedAsync(run with { Scrubber = files.ScrubberFor(vetted.Value.HeaderValues, vetted.Value.EnvValues) }, vetted.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ApiRunResponse>> RunLockedAsync(RunFacts run, VettedRun vetted, CancellationToken cancellationToken)
    {
        var lease = await StackLocks.AcquireAsync(locks, run.Request.LockTimeout, cancellationToken).ConfigureAwait(false);
        if (lease.IsError)
        {
            // A lock that stayed busy is the caller's to retry and records nothing; any other failure is this run's error.
            return lease.Error.Kind == ErrorKind.Timeout
                ? lease.Error
                : await StopAsync(run, new Stopped(vetted.Script, vetted.Host?.Origin, lease.Error), cancellationToken).ConfigureAwait(false);
        }

        var collected = new Collected(run.Scrubber);
        await using (lease.Value.ConfigureAwait(false))
        {
            await requests.RunAsync(vetted, collected, cancellationToken).ConfigureAwait(false);
            await calls.RunAsync(vetted, collected, cancellationToken).ConfigureAwait(false);
        }

        return await WriteAsync(run, Finished(run, vetted, collected), cancellationToken).ConfigureAwait(false);
    }

    // An error of the input stops the run as a refusal (exit 1); a failure of the environment (unavailable) as an errored run (exit 4). No step ran either way.
    private ValueTask<Result<ApiRunResponse>> StopAsync(RunFacts run, Stopped stopped, CancellationToken cancellationToken)
    {
        var coded = new CodedMessage(stopped.Error.Code, stopped.Error.Message);
        var summary = stopped.Error.Kind == ErrorKind.Unavailable
            ? Compose(run, new SummaryParts(stopped.Script, stopped.Host, ApiOutcome.Errored, "errored: " + stopped.Error.Message, new Collected(run.Scrubber), Refusal: null, coded, []))
            : Compose(run, new SummaryParts(stopped.Script, stopped.Host, ApiOutcome.Refused, Number.Text($"refused: {stopped.Error.Code} {Number.ShortPaths(stopped.Error.Message)}"), new Collected(run.Scrubber), coded, Error: null, []));
        return WriteAsync(run, summary, cancellationToken);
    }

    private async ValueTask<Result<ApiRunResponse>> WriteAsync(RunFacts run, ApiSummary summary, CancellationToken cancellationToken)
    {
        var written = await files.WriteDocumentAsync(run.Request.RunDirectory, summary with { Refusal = DataRunFiles.Messaged(summary.Refusal, run.Scrubber), Error = DataRunFiles.Messaged(summary.Error, run.Scrubber) }, cancellationToken).ConfigureAwait(false);
        return written.Map(static pair => new ApiRunResponse(pair.Summary, pair.Document));
    }

    private ApiSummary Finished(RunFacts run, VettedRun vetted, Collected collected)
    {
        var headline = HeadlineOf(collected);
        var outcome = collected.Error is not null ? ApiOutcome.Errored : collected.Failures.Count > 0 ? ApiOutcome.Failed : ApiOutcome.Passed;
        return Compose(run, new SummaryParts(vetted.Script, vetted.Host?.Origin, outcome, headline, collected, Refusal: null, collected.Error, vetted.PassedEnv));
    }

    private static string HeadlineOf(Collected collected)
    {
        if (collected.Error is not null)
        {
            return "errored: " + collected.Error.Message;
        }

        if (collected.Failures.Count > 0)
        {
            return $"{collected.FirstFailureText} (assertion {Number.Text(collected.FirstFailedAssertion)} of {Number.Text(collected.Assertions)})";
        }

        return collected.Assertions == 1 ? "passed: 1 assertion" : $"passed: {Number.Text(collected.Assertions)} assertions";
    }

    private ApiSummary Compose(RunFacts run, SummaryParts parts) =>
        new(
            ApiSummary.SchemaName, ApiSummary.KindName, RunId.From(RunPath.NameOf(run.Request.RunDirectory)), run.Instance, run.Started, clock.GetUtcNow(),
            run.Request.AppBuild, run.Sha256, run.Request.At, parts.Host, parts.Outcome, RecordedText.Of(run.Scrubber, parts.Headline, RecordedText.HeadlineLimit), parts.Collected.Steps,
            parts.Collected.Failures, [.. parts.Script?.DeclaredWrites ?? []], parts.PassedEnv, ApiSummary.Unseeded, SeedSha256: null, parts.Refusal, parts.Error, []);

    private sealed record RunFacts(ApiRunRequest Request, string Sha256, RunInstanceId Instance, DateTimeOffset Started, ITextScrubber Scrubber);

    private sealed record Stopped(ApiScript? Script, string? Host, Error Error);

    private sealed record SummaryParts(ApiScript? Script, string? Host, ApiOutcome Outcome, string Headline, Collected Collected, CodedMessage? Refusal, CodedMessage? Error, IReadOnlyList<string> PassedEnv);
}
