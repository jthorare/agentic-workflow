using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Updates that land while the interpreter can't decide them one by one (engine P5 review): a cancel and a takeover
/// that arrive in the same activation are both validated against the live instance, so the loop must still settle the
/// one the cancel left behind (SND-WF-003), and an 'open-request' that arrives while the instance is starting waits for
/// it instead of being refused (UI spec E-6).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class QueuedUpdateTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string Launch = "LCH-0123456789abcdef0123456789abcdef";
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(20);
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A_takeover_queued_behind_a_cancel_in_the_same_activation_fails_SND_WF_003_and_the_workflow_completes()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var options = sindri.GetRequiredService<TemporalWorkerOptions>();
        options.MaxCachedWorkflows = 0;   // the time-skipping server never moves a task off a dead sticky queue (TemporalProbes)
        var client = server.Environment.Client;
        var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-31", ct);
        WorkflowHandle<RecipeWorkflow, RecipeResult> handle;
        using (var first = new TemporalWorker(client, options))
        {
            handle = await first.ExecuteAsync(async () =>
            {
                var started = await WorkflowsHarness.Start(client, start);
                _ = await WorkflowsHarness.Until(started, static v => v.Task is not null);
                return started;
            }, ct);
        }

        // why: with no worker running, both Updates wait on the server and reach the next worker in one activation
        var cancel = new CancelRequested("human:cli", CancelOrigin.Command, Stamp('c'), Sequence: null, Parent: null);
        var takeover = new TakeoverRequested("human:cli", Stamp('d'));
        var cancelling = handle.ExecuteUpdateAsync(wf => wf.CancelAsync(cancel), new WorkflowUpdateOptions(new string('c', 32)));
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);   // the server admits the cancel first, so the worker validates and runs it before the takeover
        var takingOver = handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), new WorkflowUpdateOptions(new string('d', 32)));
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // let the server queue both

        using var second = new TemporalWorker(client, options);
        await second.ExecuteAsync(async () =>
        {
            var cancelled = await cancelling.WaitAsync(_patience, ct);
            var refused = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => takingOver.WaitAsync(_patience, ct));
            var ended = await handle.GetResultAsync().WaitAsync(_patience, ct);

            Assert.Equal("cancelled", cancelled.Instance.State);
            Assert.Equal("SND-WF-003", Assert.IsType<ApplicationFailureException>(refused.InnerException).ErrorType);
            Assert.Equal("cancelled", ended.State);
        }, ct);
    }

    [Fact]
    public async Task A_cancel_queued_beside_a_report_in_the_same_activation_loses_to_it_and_no_handler_hangs()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var options = sindri.GetRequiredService<TemporalWorkerOptions>();
        options.MaxCachedWorkflows = 0;   // the time-skipping server never moves a task off a dead sticky queue (TemporalProbes)
        var client = server.Environment.Client;
        var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-33", ct);
        WorkflowHandle<RecipeWorkflow, RecipeResult> handle;
        RunToken run;
        using (var first = new TemporalWorker(client, options))
        {
            (handle, run) = await first.ExecuteAsync(async () =>
            {
                var started = await WorkflowsHarness.Start(client, start);
                var open = await WorkflowsHarness.Until(started, static v => v.Task is not null && v.Run is not null);
                return (started, open.Run!.Value);
            }, ct);
        }

        // why: with no worker running, both Updates wait on the server and reach the next worker in one activation
        var cancel = new CancelRequested("human:cli", CancelOrigin.Command, Stamp('c'), Sequence: null, Parent: null);
        var report = Report(run);
        var cancelling = handle.ExecuteUpdateAsync(wf => wf.CancelAsync(cancel), new WorkflowUpdateOptions(new string('c', 32)));
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);   // the server admits the cancel first
        var reporting = handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('d', 32)));
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // let the server queue both

        using var second = new TemporalWorker(client, options);
        await second.ExecuteAsync(async () =>
        {
            var reported = await reporting.WaitAsync(_patience, ct);
            var refused = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => cancelling.WaitAsync(_patience, ct));
            var ended = await handle.GetResultAsync().WaitAsync(_patience, ct);

            Assert.Equal("-> done", reported.Happened);
            Assert.Equal("SND-WF-003", Assert.IsType<ApplicationFailureException>(refused.InnerException).ErrorType);
            Assert.Equal("done", ended.State);
        }, ct);
    }

    [Fact]
    public async Task An_open_request_that_arrives_while_the_instance_is_starting_waits_for_it_and_takes_it_over()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var client = server.Environment.Client;
        var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-32", ct);
        var handle = await WorkflowsHarness.Start(client, start);   // no worker yet: the instance is still starting when the Update arrives
        var open = new OpenRequested("service:engine", Launch);
        var opening = handle.ExecuteUpdateAsync(wf => wf.OpenRequestAsync(open), new WorkflowUpdateOptions(InstanceIds.OpenRequestUpdate(start.WorkflowId(), open)));
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // let the server queue it

        using var worker = new TemporalWorker(client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var taken = await opening.WaitAsync(_patience, ct);

            Assert.Equal(EngineStates.TakenOver, taken.Instance.State);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task An_open_request_for_a_malformed_launch_is_refused_while_the_instance_is_starting()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var client = server.Environment.Client;
        var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-33", ct);
        var handle = await WorkflowsHarness.Start(client, start);
        var open = new OpenRequested("service:engine", "LCH-not-a-launch");
        var opening = handle.ExecuteUpdateAsync(wf => wf.OpenRequestAsync(open), new WorkflowUpdateOptions(InstanceIds.OpenRequestUpdate(start.WorkflowId(), open)));
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // let the server queue it

        using var worker = new TemporalWorker(client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var refused = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => opening.WaitAsync(_patience, ct));

            Assert.Equal("SND-WF-003", Assert.IsType<ApplicationFailureException>(refused.InnerException).ErrorType);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    private static StepCompleted Report(RunToken run)
    {
        var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
        return new StepCompleted("human:cli", run, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
    }

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));
}
