using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows;
using Aw.Sindri.Tests.Workflows.Spike;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.OperatorService.V1;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// The desk wait's timeout on the time-skipping server (engine spec W20, §3.8; rev 8.1 §5.3, finding F1): the wait is
/// the state's timeout raised to 'workflow.deskTimeout' (2d), and its expiry parks with 'timeout' on the same run. A
/// 'then: retry' state never retries in desk mode. Lane decision (P4 unit (c)): W20 and §3.8 win over §5.3's
/// 'step.failed(timeout)'.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DeskTimeoutTests(TimeSkippingServer server) : IClassFixture<TimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 240_000)]
    public async Task Desk_timeout_never_retries()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TemporalServerFixture.Start(ServerKind.TimeSkipping);   // its own server: after the other test on the class's, the server's clock sits days ahead and the first skip can wait in wall clock
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        await RegisterAttributes(env);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = worker.ExecuteAsync(stop.Token);
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-7", ct));
        var desk = new DeskInstance(sindri, handle);
        try
        {
            _ = await desk.Until(static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            var done = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32))).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal("-> checks", done.Happened);   // 'checks': timeout { after: 30m, then: retry }

            await WorkflowsHarness.Skip(env, TimeSpan.FromHours(1), ct);
            var waiting = await desk.Until(static v => v.Task is not null);
            await WorkflowsHarness.Skip(env, TimeSpan.FromDays(2), ct);
            var parked = await desk.Until(static v => v.State == "parked");
            await WorkflowsHarness.Skip(env, TimeSpan.FromDays(5), ct);
            var later = await desk.Until(static v => v.State == "parked");

            Assert.Equal(("checks", "2.1"), (waiting.State, waiting.Run?.Value));
            Assert.Equal((ParkReason.Timeout, "checks ran past 2d", "2.1"), (parked.Question?.Reason, parked.Question?.Question, parked.Run?.Value));
            Assert.Equal((ParkReason.Timeout, "2.1"), (later.Question?.Reason, later.Run?.Value));
            Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition"], await desk.Events());

            await AssertParkRowDetail(desk);
        }
        finally
        {
            await handle.TerminateAsync("test over");
            await stop.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }

    [Fact(Timeout = 120_000)]
    public async Task A_complete_for_the_open_run_of_a_parked_desk_step_is_rejected_until_it_is_resumed()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        await RegisterAttributes(env);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = worker.ExecuteAsync(stop.Token);
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-8", ct));
        var desk = new DeskInstance(sindri, handle);
        try
        {
            _ = await desk.Until(static v => v.Task is not null);
            await WorkflowsHarness.Skip(env, TimeSpan.FromDays(3), ct);
            var parked = await desk.Until(static v => v.State == "parked");
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", parked.Run!.Value, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));

            var rejected = await Assert.ThrowsAnyAsync<Exception>(() => Complete(handle, report, 'c', ct));
            var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('a', 32)), new string('b', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('a', 32))).WaitAsync(TimeSpan.FromSeconds(30), ct);
            var done = await Complete(handle, report, 'd', ct);

            Assert.Equal("SND-WF-003", (rejected.InnerException as Temporalio.Exceptions.ApplicationFailureException)?.ErrorType);
            Assert.Equal("-> checks", done.Happened);
            Assert.Contains("answer it first: sindri workflow status", rejected.InnerException?.Message, StringComparison.Ordinal);
        }
        finally
        {
            await handle.TerminateAsync("test over");
            await stop.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }

    /// <summary>The park row has a detail too: the deadline's key and actor, and the park as the post-state suspension.</summary>
    private static async Task AssertParkRowDetail(DeskInstance desk)
    {
        var parkRow = (await desk.Payloads("workflow.transition"))[^1].GetProperty("detail");
        var suspension = parkRow.GetProperty("delta").GetProperty("suspension");
        Assert.Equal("service:workflow.timers", parkRow.GetProperty("triggerActor").GetString());
        Assert.Contains(":timer:", parkRow.GetProperty("triggerId").GetString(), StringComparison.Ordinal);
        Assert.Equal("timeout", suspension.GetProperty("reason").GetString(), ignoreCase: true);
        Assert.Equal("park", suspension.GetProperty("kind").GetString(), ignoreCase: true);
    }

    private static Task<CommandResult> Complete(WorkflowHandle<RecipeWorkflow> handle, StepCompleted report, char id, CancellationToken ct) =>
        handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string(id, 32))).WaitAsync(TimeSpan.FromSeconds(30), ct);

    internal static async Task RegisterAttributes(Temporalio.Testing.WorkflowEnvironment env)
    {
        var attributes = new AddSearchAttributesRequest { Namespace = env.Client.Options.Namespace };
        attributes.SearchAttributes.Add(SindriTemporal.SearchAttributes.ToDictionary(static a => a.Name, static a => a.Type, StringComparer.Ordinal));
        try
        {
            _ = await env.Client.OperatorService.AddSearchAttributesAsync(attributes);   // what serve registers on the dev server
        }
        catch (Temporalio.Exceptions.RpcException e) when (e.Message.Contains("already exists", StringComparison.Ordinal))
        {
            // the class fixture's server keeps the attributes the other test registered
        }
    }
}
