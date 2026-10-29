using Aw.Context.Core.Models;
using Aw.Judge.Core.Providers;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Features.ResumeJob;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The router end to end (model routing spec §12): fake CLIs, the event writer over the in-memory ledger, and a job that parks on the real <see cref="JobWaits"/> and is woken by ResumeJob.</summary>
[Lane(TestLane.Integration)]
public sealed class RoutedModelPortEndToEndTests
{
    private static readonly JobId _job = JobId.From("JOB-" + new string('b', 32));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly SliceHarness _h = new();
    private readonly LineProgress _progress = new();
    private readonly FakeJobContextAccessor _jobs = new();

    private (RoutedModelPort Port, JobWaits Waits) Rig(params ScriptedCli[] clis)
    {
        var waits = new JobWaits(_h.Clock);
        var log = new LedgerModelCallLog(_h.Held, _h.Store, _h.Events, new ListLogger<LedgerModelCallLog>());
        return (new RoutedModelPort(() => new ScriptedCatalog(clis), new FixedModels(new Aw.Context.Core.Engine.ModelsSettings()), _jobs, log, _h.Ids, _h.Clock), waits);
    }

    private IDisposable InJob(JobWaits waits) => _jobs.Enter(new JobContext(_job, JobKind.From("BuildContextMap"), _progress, new ParkedOnJobWaits(waits, _job)));

    private List<ContextModelCalled> Written() =>
        [.. _h.Store.Events.Where(static e => string.Equals(e.Envelope.Type, EventTypes.ContextModelCalled, StringComparison.Ordinal)).Select(e => _h.Events.Payload<ContextModelCalled>(e).Value)];

    private static ModelRequest Ask() => RouterRig.Request(ModelPurpose.Adjudication);

    [Fact]
    public async Task Claude_limited_and_Codex_answering_leaves_one_event_with_two_tries_and_the_next_call_skips_Claude()
    {
        _ = await _h.HoldLease();
        var claude = new ScriptedCli(ProviderName.ClaudeCli, RouterRig.Limit());
        var (port, waits) = Rig(claude, new ScriptedCli(ProviderName.CodexCli, RouterRig.Answered("one"), RouterRig.Answered("two")));
        using var job = InJob(waits);

        _ = await port.Ask(Ask(), Token);
        _ = await port.Ask(Ask(), Token);

        var events = Written();
        Assert.Equal(2, events.Count);
        Assert.Equal([ModelCallOutcome.Limit, ModelCallOutcome.Answered], events[0].Tries.Select(static t => t.Outcome), StringComparer.Ordinal);
        Assert.Equal([ModelCallOutcome.Skipped, ModelCallOutcome.Answered], events[1].Tries.Select(static t => t.Outcome), StringComparer.Ordinal);
        _ = Assert.Single(claude.Asked);
        Assert.All(events, e => Assert.Equal(_job, e.Job));
    }

    [Fact]
    public async Task Every_harness_limited_parks_the_job_and_ResumeJob_wakes_it_to_an_answer_from_Claude()
    {
        _ = await _h.HoldLease();
        var claude = new ScriptedCli(ProviderName.ClaudeCli, RouterRig.Limit(), RouterRig.Answered("back"));
        var (port, waits) = Rig(claude, new ScriptedCli(ProviderName.CodexCli, RouterRig.Limit()));
        using var job = InJob(waits);

        var asking = port.Ask(Ask(), Token).AsTask();
        while (!waits.IsWaiting(_job, "model-limit"))
        {
            await Task.Delay(5, Token);
        }

        using var human = _h.Callers.Enter(Aw.Sindri.Testing.A.Caller().Human().Build());
        var resumed = await new ResumeJobService(waits, _h.Store, _h.Callers).Handle(new ResumeJobRequest(_job), Token);
        var answer = await asking;

        Assert.True(resumed.IsSuccess);
        Assert.Equal("claude-code", answer.Value.Harness);
        Assert.Equal(2, claude.Asked.Count);
        Assert.Contains("Paused: claude-code at its usage limit; codex at its usage limit", Assert.Single(_progress.Lines), StringComparison.Ordinal);
        Assert.Equal(2, Written().Count);   // the pass that parked, then the pass that answered
    }
}
