using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Jobs;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>The jobs view in views.db (API P6 unit B): rows the real projection folds are read back by id and as the running list; the list's order and filters are the JobViewsContract's.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteJobViewsTests : IDisposable
{
    private static readonly JobKind _run = JobKind.From("RunService");
    private static readonly JobKind _sync = JobKind.From("SyncSources");
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private readonly SqliteViews _views;
    private long _sequence;

    public SqliteJobViewsTests()
    {
        _views = new SqliteViews(Path.Combine(_home.Path, "home"));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SqliteJobViews Jobs => new(_views.Reader);

    public void Dispose()
    {
        _views.Dispose();
        _home.Dispose();
    }

    private static JobId Id(int n) => JobId.From("JOB-" + n.ToString("x32", CultureInfo.InvariantCulture));

    private async Task Apply<TPayload>(string type, TPayload payload)
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        var applied = (await _views.Store.Apply([Recorded.Of(_h.Events, type, payload, ++_sequence)], [new JobsProjection(_h.Events)], _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
    }

    private Task Start(int n, JobKind kind, string key) =>
        Apply(EventTypes.JobStarted, new JobStarted(_h.Ids.New<CommandId>(), Id(n), kind, JobKey.From(key), Item: null, Repo: null, Service: null));

    private Task Finish(int n) =>
        Apply(EventTypes.JobFinished, new JobFinished(_h.Ids.New<CommandId>(), Id(n), JobOutcome.Succeeded, "done", Code: null, Result: null));

    [Fact]
    public async Task Jobs_view_reads_running_and_by_id()
    {
        await Start(1, _run, "heartbeat");
        await Start(2, _sync, "all");
        await Start(3, _run, "index");
        await Finish(1);

        var one = (await Jobs.Get(Id(1), Token)).Value;
        var missing = (await Jobs.Get(Id(9), Token)).Value;
        var running = (await Jobs.Running(Token)).Value;

        Expect.Equal(JobState.Succeeded, one!.State);
        Assert.Null(missing);
        Expect.Sequence([Id(2), Id(3)], running.Select(r => r.Id));
    }

    [Fact]
    public async Task A_rebuild_from_the_ledger_gives_the_same_model_totals_and_waiting_signal()
    {
        var job = Id(1);
        var other = Id(2);
        var started = new JobStarted(_h.Ids.New<CommandId>(), job, _run, JobKey.From("index"), Item: null, Repo: null, Service: null);
        var otherStarted = new JobStarted(_h.Ids.New<CommandId>(), other, _sync, JobKey.From("all"), Item: null, Repo: null, Service: null);
        IReadOnlyList<LedgerEvent> ledger =
        [
            Recorded.Of(_h.Events, EventTypes.JobStarted, started, 1),
            Recorded.Of(_h.Events, EventTypes.JobStarted, otherStarted, 2),
            Recorded.Of(_h.Events, EventTypes.ContextModelCalled, ModelCallRows.Answered(job, "call-1", "codex", "gpt-6-luna", 100, 50), 3),
            Recorded.Of(_h.Events, EventTypes.ContextModelCalled, ModelCallRows.FellBack(job, "call-2", "claude-code", "codex", "gpt-6-luna", 30, 10), 4),
            Recorded.Of(_h.Events, EventTypes.ContextModelCalled, ModelCallRows.Answered(job: null, "call-3", "codex", "gpt-6-luna", 5, 5), 5),
            Recorded.Of(_h.Events, EventTypes.JobWaiting, new JobWaiting(_h.Ids.New<CommandId>(), job, "model-limit"), 6),
            Recorded.Of(_h.Events, EventTypes.JobWaiting, new JobWaiting(_h.Ids.New<CommandId>(), other, "model-limit"), 7),
            Recorded.Of(_h.Events, EventTypes.JobResumed, new JobResumed(_h.Ids.New<CommandId>(), other, "model-limit"), 8),
        ];
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        IReadOnlyList<IProjection> projections = [new JobsProjection(_h.Events)];
        foreach (var recorded in ledger)
        {
            Assert.Null((await _views.Store.Apply([recorded], projections, _h.Clock.GetUtcNow(), Token)).Value.Stall);
        }

        var incremental = Json((await _views.Reader.List<JobRow>(ViewNames.Jobs, Token)).Value);
        _ = (await _views.Store.Reset(ViewNames.All, ViewSchema.Version, _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null((await _views.Store.Apply(ledger, projections, _h.Clock.GetUtcNow(), Token)).Value.Stall);
        _ = (await _views.Store.FinishRebuild(Token)).Value;
        var rebuilt = Json((await _views.Reader.List<JobRow>(ViewNames.Jobs, Token)).Value);

        Expect.Equal(incremental, rebuilt);
        var row = (await _views.Reader.Get<JobRow>(ViewNames.Jobs, job.Value, Token)).Value!;
        Expect.Equal((2, 0, 130L, 60L, "model-limit"), (row.Models!.Calls, row.Models.FailedCalls, row.Models.InputTokens, row.Models.OutputTokens, row.WaitingOn));
        Assert.Null((await _views.Reader.Get<JobRow>(ViewNames.Jobs, other.Value, Token)).Value!.WaitingOn);
    }

    private static string Json(IEnumerable<JobRow> rows) => string.Join('\n', rows.Select(static r => System.Text.Json.JsonSerializer.Serialize(r, SliceHarness.Json)));

    [Fact]
    public async Task A_missing_views_db_reads_as_no_jobs()
    {
        Assert.Null((await Jobs.Get(Id(1), Token)).Value);
        Assert.Empty((await Jobs.Running(Token)).Value);
        Assert.Empty((await Jobs.List(state: null, kind: null, Token)).Value);
    }
}
