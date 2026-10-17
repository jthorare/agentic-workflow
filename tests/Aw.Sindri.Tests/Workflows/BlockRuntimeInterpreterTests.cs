using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The block runtime in the interpreter (DT spec §8.2): the dev evidence route's judge state runs a registered block Step in an activity, once, and a pre-DT-J history replays on the desk path.</summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class BlockRuntimeInterpreterTests : IDisposable
{
    private const string Route = "workflow.dev-evidence-route";
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_block_step_completes_without_a_desk_report()
    {
        var fake = FakeBlockStep.Completing("ui");
        var ct = TestContext.Current.CancellationToken;
        await Run(fake, maxDuration: null, async (handle, sindri) =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.State == "evidence-ui");
            var row = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
                .Last(static e => e.Envelope.Type == "workflow.transition");
            using var payload = JsonDocument.Parse(row.Envelope.Payload);

            Assert.Equal(("evidence-ui", 1), (view.State, fake.Runs));
            Assert.NotNull(view.Task);   // the next state's desk task
            Assert.Equal(("block", "JD-1", "service:block:step.judge.choose"), (payload.RootElement.GetProperty("via").GetString(), payload.RootElement.GetProperty("decision").GetString(), row.Envelope.Actor));
            Assert.Equal("block", payload.RootElement.GetProperty("detail").GetProperty("via").GetString());
            Assert.Equal("rules", fake.Last!.Arguments["providers"].EnumerateArray().Last().GetString());   // the effective provider list (DT-J plan Q8), rules last
            Assert.Equal("fact:diff", fake.Last!.Facts.Keys.Select(static k => k.Value).Order(StringComparer.Ordinal).Last());
            await Task.Delay(1, ct);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_block_step_past_MaxDuration_is_step_failed_timeout_and_goes_to_the_desk_twin()
    {
        var fake = FakeBlockStep.Hanging();
        var ct = TestContext.Current.CancellationToken;
        await Run(fake, TimeSpan.FromSeconds(2), async (handle, sindri) =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.State == "route-evidence-desk");
            var failed = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
                .Select(static e => JsonDocument.Parse(e.Envelope.Payload))
                .Single(static p => p.RootElement.TryGetProperty("trigger", out var t) && t.GetString() == "step.failed");

            Assert.NotNull(view.Task);
            Assert.Equal(1, fake.Runs);
            Assert.Equal("timeout", failed.RootElement.GetProperty("detail").GetProperty("reason").GetString());
            await WaitFor(() => fake.Cancelled == 1, ct, seconds: 12);   // the run past its bound is cancelled, not left running
            await Task.Delay(1, ct);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task The_block_activity_is_attempted_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = new FakeBlockStep(static (_, _) => throw new InvalidOperationException("the judge fell over"));
        await Run(fake, maxDuration: null, async (handle, host) =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "route-evidence-desk");
            await Task.Delay(TimeSpan.FromSeconds(2), ct);   // a retry would be a second run by now

            Assert.Equal(1, fake.Runs);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Cancel_while_a_block_step_runs_cancels_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Hanging();
        await Run(fake, maxDuration: null, async (handle, host) =>
        {
            await WaitFor(() => fake.Runs > 0, ct);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);   // the Step has heartbeated at least once: Temporal delivers a cancel in a heartbeat's answer
            var stamp = new CommandStamp(CommandNonce.From(new string('c', 32)), new string('1', 64));
            var cancelled = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(new CancelRequested("human:cli", CancelOrigin.Command, stamp, Sequence: null, Parent: null)), new WorkflowUpdateOptions(new string('c', 32)));

            Assert.Equal("cancelled", cancelled.Instance.State);
            await WaitFor(() => fake.Cancelled == 1, ct, seconds: 12);   // the heartbeat timeout's throttle (8 s), not the 30 s default
        });
    }

    [Theory(Timeout = 120_000)]
    [InlineData("human:cli")]
    [InlineData("agent:session")]
    public async Task A_desk_report_on_a_block_run_is_refused_at_once_and_blocks_no_later_report(string actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Hanging();
        await Run(fake, maxDuration: null, async (handle, host) =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "route-evidence");
            await WaitFor(() => fake.Runs > 0, ct);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted(actor, view.Run!.Value, OutcomeName.From("ui"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:decision")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));

            var first = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32))).WaitAsync(TimeSpan.FromSeconds(20), ct));
            var second = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('b', 32))).WaitAsync(TimeSpan.FromSeconds(20), ct));

            Assert.Equal(("SND-WF-003", "SND-WF-003"), (Assert.IsType<ApplicationFailureException>(first.InnerException).ErrorType, Assert.IsType<ApplicationFailureException>(second.InnerException).ErrorType));
            Assert.Equal(0, fake.Cancelled);   // the engine's run goes on
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Takeover_during_a_block_run_cancels_it_and_a_handback_runs_it_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.HangingThenCompleting("ui");
        await Run(fake, maxDuration: null, async (handle, host) =>
        {
            await WaitFor(() => fake.Runs > 0, ct);
            var takeover = new TakeoverRequested("human:cli", new CommandStamp(CommandNonce.From(new string('1', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), new WorkflowUpdateOptions(new string('1', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "taken-over");
            await WaitFor(() => fake.Cancelled == 1, ct);

            var handback = new HandbackRequested("human:cli", new CommandStamp(CommandNonce.From(new string('2', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.HandbackAsync(handback), new WorkflowUpdateOptions(new string('2', 32)));
            var view = await WorkflowsHarness.Until(handle, static v => v.State == "evidence-ui");

            Assert.Equal((2, "evidence-ui"), (fake.Runs, view.State));
        });
    }

    private async Task Run(FakeBlockStep fake, TimeSpan? maxDuration, Func<WorkflowHandle<RecipeWorkflow, RecipeResult>, IServiceProvider, Task> body)
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Replacing = services => Register(services, fake, maxDuration);
        _harness.Recipe(Route, WorkflowsHarness.Shipped("dev/workflow.dev-evidence-route.yaml"));
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var sindri = _harness.Sindri(_harness.Replacing);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, Route, "T-1", ct);
            var inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:change")] = new InputFact("change.txt", _harness.Fact("Add a button to the settings page")),
                [FactKey.From("fact:diff")] = new InputFact("diff.txt", _harness.Fact("https://example.test/pr/1")),
            };
            var handle = await WorkflowsHarness.Start(env.Client, start with { Inputs = inputs });
            await body(handle, sindri);
            var hist = await handle.FetchHistoryAsync();
            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(hist, throwOnReplayFailure: false, ct);
            Assert.Null(replayed.ReplayFailure);
            if ((await handle.DescribeAsync()).Status == Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Running)
            {
                await handle.TerminateAsync("test over");
            }
        }, ct);
    }

    private static void Register(IServiceCollection services, FakeBlockStep fake, TimeSpan? maxDuration)
    {
        _ = services.Replace(ServiceDescriptor.Singleton(new BlockStepRuntimes([new BlockStepRegistration(_judge)])));
        _ = services.AddKeyedSingleton<IBlockStep>(_judge.Value, fake);
        if (maxDuration is { } max)
        {
            _ = services.Replace(ServiceDescriptor.Singleton(provider => new BlockCatalog(
                provider.GetServices<BlockDescriptor>().Select(d => d.Id == _judge ? d with { MaxDuration = max } : d),
                provider.GetServices<EventDescriptor>())));
        }
    }

    private static async Task WaitFor(Func<bool> condition, CancellationToken ct, int seconds = 20)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(seconds);
        while (!condition() && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(condition());
    }
}
