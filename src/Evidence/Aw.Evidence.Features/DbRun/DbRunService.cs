using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Locks;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.DbRun;

/// <summary>
/// Runs a database script read-only: clears the run directory (so no exit leaves an earlier summary), reads and checks the script, vets the
/// write statements, the database target, the goldens and the seed (refusals take no lock), then holds the QA-stack and heavy-job locks while
/// it opens the database and runs the queries, and writes a scrubbed summary.json on every path once the script has been read except a lock
/// that stayed busy (exit 75). An error of the input or a refusal by the database is exit 1; a failure of the environment's I/O is exit 4.
/// </summary>
internal sealed class DbRunService(DataRunFiles files, DbVetting vetting, DbQueryRunner queries, IMachineLocks locks, TimeProvider clock, IIdGenerator ids)
    : IService<DbRunRequest, DbRunResponse>
{
    public async ValueTask<Result<DbRunResponse>> Handle(DbRunRequest request, CancellationToken cancellationToken)
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
        var script = files.ParseDb(request.ScriptPath, bytes.Value);
        if (script.IsError)
        {
            return await StopAsync(run, script: null, script.Error, cancellationToken).ConfigureAwait(false);
        }

        var vetted = await vetting.VetAsync(request, script.Value, cancellationToken).ConfigureAwait(false);
        return vetted.IsError
            ? await StopAsync(run, script.Value, vetted.Error, cancellationToken).ConfigureAwait(false)
            : await RunLockedAsync(run with { Scrubber = files.ScrubberFor([], vetted.Value.SecretValues) }, vetted.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<DbRunResponse>> RunLockedAsync(RunFacts run, VettedDb vetted, CancellationToken cancellationToken)
    {
        var lease = await StackLocks.AcquireAsync(locks, run.Request.LockTimeout, cancellationToken).ConfigureAwait(false);
        if (lease.IsError)
        {
            // A lock that stayed busy is the caller's to retry and records nothing; any other failure is this run's error.
            return lease.Error.Kind == ErrorKind.Timeout ? lease.Error : await StopAsync(run, vetted.Script, lease.Error, cancellationToken).ConfigureAwait(false);
        }

        var collected = new DbCollected(run.Scrubber);
        Result<Unit> opened;
        await using (lease.Value.ConfigureAwait(false))
        {
            opened = await queries.RunAsync(vetted, collected, cancellationToken).ConfigureAwait(false);
        }

        if (opened.IsError)
        {
            return await StopAsync(run, vetted.Script, opened.Error, cancellationToken).ConfigureAwait(false);
        }

        var scrubbed = run with { Scrubber = collected.Scrubber };
        return await WriteAsync(scrubbed, Compose(scrubbed, vetted.Script, collected, vetted.SeedSha256), cancellationToken).ConfigureAwait(false);
    }

    // An error of the input or a refusal stops the run as exit 1; a failure of the environment (unavailable) as exit 4. No query ran either way.
    private ValueTask<Result<DbRunResponse>> StopAsync(RunFacts run, DbScript? script, Error error, CancellationToken cancellationToken)
    {
        var coded = new CodedMessage(error.Code, run.Scrubber.Scrub(error.Message));
        var collected = new DbCollected(run.Scrubber);
        if (error.Kind == ErrorKind.Unavailable)
        {
            collected.Error = coded;
        }
        else
        {
            collected.Refusal = coded;
        }

        return WriteAsync(run, Compose(run, script, collected, seedSha256: null), cancellationToken);
    }

    private async ValueTask<Result<DbRunResponse>> WriteAsync(RunFacts run, DbSummary summary, CancellationToken cancellationToken)
    {
        var written = await files.WriteDocumentAsync(run.Request.RunDirectory, summary with { Refusal = DataRunFiles.Messaged(summary.Refusal, run.Scrubber), Error = DataRunFiles.Messaged(summary.Error, run.Scrubber) }, cancellationToken).ConfigureAwait(false);
        return written.Map(static pair => new DbRunResponse(pair.Summary, pair.Document));
    }

    private DbSummary Compose(RunFacts run, DbScript? script, DbCollected collected, string? seedSha256)
    {
        var outcome = collected.Refusal is not null ? ApiOutcome.Refused : collected.Error is not null ? ApiOutcome.Errored : collected.Failures.Count > 0 ? ApiOutcome.Failed : ApiOutcome.Passed;
        var engine = script is null ? null : script.Connection is not null ? DatabaseEntry.Postgres : "sqlite";
        return new DbSummary(
            DbSummary.SchemaName, DbSummary.KindName, RunId.From(RunPath.NameOf(run.Request.RunDirectory)), run.Instance, run.Started, clock.GetUtcNow(),
            run.Request.AppBuild, run.Sha256, run.Request.At, engine, script?.Connection ?? script?.Sqlite, outcome,
            RecordedText.Of(run.Scrubber, Headline(outcome, collected), RecordedText.HeadlineLimit), collected.Steps, collected.Failures,
            seedSha256 is null ? DbSummary.Unseeded : DbSummary.Seeded, seedSha256, collected.Refusal, collected.Error, []);
    }

    private static string Headline(ApiOutcome outcome, DbCollected collected) => outcome switch
    {
        ApiOutcome.Refused => Number.Text($"refused: {collected.Refusal!.Code} {Number.ShortPaths(collected.Refusal.Message)}"),   // why: a refused outcome has its refusal
        ApiOutcome.Errored => "errored: " + collected.Error!.Message,   // why: an errored outcome has its error
        ApiOutcome.Failed => $"{collected.FirstFailureText} (assertion {Number.Text(collected.FirstFailedAssertion)} of {Number.Text(collected.Assertions)})",
        ApiOutcome.Passed => collected.Assertions == 1 ? "passed: 1 assertion" : $"passed: {Number.Text(collected.Assertions)} assertions",
        _ => "errored",
    };

    private sealed record RunFacts(DbRunRequest Request, string Sha256, RunInstanceId Instance, DateTimeOffset Started, ITextScrubber Scrubber);
}
