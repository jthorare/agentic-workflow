using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Workflows.Effects;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Common;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Linked instances, the phases of a line (LN-WF1, line spec §8.2): a linked instance that parks, resumes and ends tells its line in that order
/// ('line.phaseChanged' from the engine), it is no root in the list, and the cancel of its line reaches it while it is live.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class LinkedInstanceTests : IDisposable
{
    private const string Phase = "workflow.t-phase";
    private const string LineJson = """{"line":"L1","phase":"p1","base":"origin/main","run":"line-1"}""";
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 180_000)]
    public async Task A_linked_phase_tells_its_line_it_parked_resumed_and_ended_in_that_order()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _harness.Approve(Recipe());
        await using var server = new SindriTimeSkippingServer();
        await server.InitializeAsync();
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var tree = new ChildTree(sindri, server.Environment, _harness);
            var phase = await WorkflowsHarness.Start(server.Environment.Client, await LinkedStart(sindri, "T-81", ct));
            _ = await WorkflowsHarness.Until(phase, static v => v.State == "waiting");
            await tree.PastTheHour();
            _ = await WorkflowsHarness.Until(phase, static v => v.State == "parked");
            _ = await ChildTree.Resume(phase);
            _ = await WorkflowsHarness.Until(phase, static v => v.State == "waiting");
            await ChildTree.Go(phase);
            var result = await phase.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), ct);
            var reports = await Reports(sindri, 3, TimeSpan.FromSeconds(30));

            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
            Assert.Equal(["parked", "resumed", "ended"], reports.Select(static r => r.Outcome), StringComparer.Ordinal);
            Assert.All(reports, r => Assert.Equal(("L1", "p1", 1, LinkedAddress("T-81")), (r.Event.GetProperty("line").GetString(), r.Event.GetProperty("phase").GetString(), r.Event.GetProperty("attempt").GetInt32(), r.Event.GetProperty("instance").GetString())));
            Assert.Equal("timeout", reports[0].Event.GetProperty("parkReason").GetString());
            Assert.Equal("resolved", reports[2].Event.GetProperty("ended").GetString());
            Assert.Equal(3, reports.Select(static r => r.Identity).Distinct(StringComparer.Ordinal).Count());
            Assert.Null((await InterpreterEvalTests.Replay(phase, ct)).ReplayFailure);
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_resume_report_waits_for_the_park_report_while_the_park_notice_is_slow()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _harness.Approve(Recipe());
        var notice = new GatedNotice(new FakeDeskEffect());
        await using var server = new SindriTimeSkippingServer();
        await server.InitializeAsync();
        await using var sindri = _harness.Sindri(notice.Replace);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var tree = new ChildTree(sindri, server.Environment, _harness);
            var phase = await WorkflowsHarness.Start(server.Environment.Client, await LinkedStart(sindri, "T-83", ct));
            _ = await WorkflowsHarness.Until(phase, static v => v.State == "waiting");
            var skipped = tree.PastTheHour();   // the server's clock stands still while the held notice runs, so this ends only once it is released
            _ = await WorkflowsHarness.Until(phase, static v => v.State == "parked");
            _ = await ChildTree.Resume(phase);
            _ = await WorkflowsHarness.Until(phase, static v => v.State == "waiting");
            var early = await Reports(sindri, 1, TimeSpan.FromSeconds(2));   // the park notice is held, so the park report waits behind it: a report that lands now is the resume's, ahead of it

            notice.Release();
            await skipped;
            await ChildTree.Go(phase);
            _ = await phase.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), ct);
            var reports = await Reports(sindri, 3, TimeSpan.FromSeconds(30));

            Assert.Empty(early);
            Assert.Equal(["parked", "resumed", "ended"], reports.Select(static r => r.Outcome), StringComparer.Ordinal);
        }, ct);
    }

    [Fact]
    public async Task A_history_recorded_before_the_report_order_marker_replays_with_its_reports_concurrent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/linked-phase-pre-report-order.json")!;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>())
            .ReplayWorkflowAsync(WorkflowHistory.FromJson(LinkedAddress("T-83"), json), throwOnReplayFailure: false, ct);

        Assert.Null(replayed.ReplayFailure);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_linked_instance_is_no_root_and_a_cancelled_line_cancels_its_live_phase()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _harness.Approve(Recipe());
        _ = _harness.Approve(ChildRecipes.Child());
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await Served.Start(_harness.Home, env, ct);
        var sindri = served.Sindri;
        var tree = new ChildTree(sindri, env, _harness);

        var line = await tree.Start("workflow.t-child", "T-82");
        var phase = await WorkflowsHarness.Start(env.Client, await LinkedStart(sindri, "T-82", ct));
        _ = await WorkflowsHarness.Until(line, static v => v.State == "waiting");
        _ = await WorkflowsHarness.Until(phase, static v => v.State == "waiting");

        Assert.True((await line.DescribeAsync()).TypedSearchAttributes.Get(SindriTemporal.RootKey));
        Assert.False((await phase.DescribeAsync()).TypedSearchAttributes.Get(SindriTemporal.RootKey));

        _ = await ChildTree.Cancel(line);
        var cancelled = await phase.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), ct);

        Assert.Equal(("cancelled", "cancelled"), (cancelled.State, cancelled.Outcome));
        Assert.Equal(Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Completed, (await phase.DescribeAsync()).Status);   // its own cancelled, not a terminate
        var (outcome, _, report) = (await Reports(sindri, 1, TimeSpan.FromSeconds(30))).Single();
        Assert.Equal(("ended", "cancelled"), (outcome, report.GetProperty("ended").GetString()));   // the cancelled end reaches the line too
    }

    private static string LinkedAddress(string item) => InstanceAddress.From(item + "/t-child/1").Linked(RecipeId.From(Phase), "p1", 1).Value;

    /// <summary>The linked phase of the line 'T-nn/t-child/1', carrying the line context.</summary>
    private async Task<InstanceStart> LinkedStart(IServiceProvider sindri, string item, CancellationToken ct)
    {
        var line = InstanceAddress.From(item + "/t-child/1");
        return await WorkflowsHarness.RecipeStart(sindri, Phase, item, ct) with
        {
            Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:lineContext")] = new InputFact("line.json", _harness.Fact(LineJson)) },
            ChildAddress = line.Linked(RecipeId.From(Phase), "p1", 1),
            ParentId = WorkflowInstanceId.Of(line),
        };
    }

    /// <summary>The 'line.phaseChanged' replies in the ledger, oldest first, once there are <paramref name="count"/>.</summary>
    private static async Task<IReadOnlyList<(string Outcome, string Identity, JsonElement Event)>> Reports(IServiceProvider sindri, int count, TimeSpan patience)
    {
        var events = sindri.GetRequiredService<EventFactory>();
        var deadline = RealTime.NowDateTime + patience;
        while (true)
        {
            var found = new List<(string, string, JsonElement)>();
            foreach (var recorded in await EffectRows.Events(sindri, "line.phaseChanged"))
            {
                var reply = EffectRows.Payload<EffectReply>(events, recorded);
                var fact = reply.Facts!["fact:phaseEvent"];
                var stored = await sindri.GetRequiredService<IFactContents>().Read(new FactValue(fact.Ref, fact.Sha256, fact.HeadSha), TestContext.Current.CancellationToken);
                found.Add((reply.Outcome, reply.Effect, JsonDocument.Parse(stored.Value).RootElement.Clone()));
            }

            if (found.Count >= count || RealTime.NowDateTime > deadline)
            {
                return found;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The engine's park notice held until <see cref="Release"/>, as a slow desk notification is; a recipe effect passes through.</summary>
    private sealed class GatedNotice(FakeDeskEffect inner) : IEffectKind
    {
        private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _open.TrySetResult();

        public void Replace(IServiceCollection services) =>
            services.AddKeyedScoped<IEffectKind>("effect.notify.desk", (_, _) => this);

        public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
        {
            if (context.FromEngine)
            {
                await _open.Task.WaitAsync(cancellationToken);
            }

            return await inner.Dispatch(context, cancellationToken);
        }
    }

    private static string Recipe() => string.Join('\n',
        [
            "schema: 1",
            "id: " + Phase,
            "version: 1",
            "inputs:",
            "  - { key: fact:lineContext, label: \"Line context\", type: payload, kind: payload }",
            "initial: waiting",
            "states:",
            "  waiting: { kind: wait, waitFor: { event: t.go }, timeout: { after: 1h, then: park }, next: \"Waiting for {item}.\" }",
            "  done:    { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: waiting, to: done, on: wait.satisfied }",
            "evals: []",
        ]);
}
