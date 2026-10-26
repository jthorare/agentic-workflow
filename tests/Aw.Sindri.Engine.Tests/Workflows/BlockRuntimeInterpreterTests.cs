using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
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
public sealed class BlockRuntimeInterpreterTests(BlockRuntimeDevServer server) : IClassFixture<BlockRuntimeDevServer>, IDisposable
{
    private const string Route = "workflow.dev-evidence-route";
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");
    private readonly WorkflowsHarness _harness = new();
    private static int _items;

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
            Assert.Equal(JudgeProviders.Default, [.. fake.Last!.Arguments["providers"].EnumerateArray().Select(static p => p.GetString()!)]);   // the effective provider list (DT-J plan Q8): no profile key, so the defaults, rules last
            Assert.Equal("fact:diff", fake.Last!.Facts.Keys.Select(static k => k.Value).Order(StringComparer.Ordinal).Last());
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
            Assert.NotNull(view.Task);
            Assert.Equal(1, fake.Runs);
            Assert.Equal(("timeout", "block"), await FailedRow(sindri, ct));   // a failed block run says via block too
            await WaitFor(() => fake.Cancelled == 1, ct, seconds: 15);   // the run past its bound is cancelled, not left running (the answer to the Step's next throttled heartbeat, about 8 s)
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_block_step_without_a_maxDuration_is_given_no_short_bound()
    {
        var fake = FakeBlockStep.Hanging();
        var ct = TestContext.Current.CancellationToken;
        await Run(fake, maxDuration: null, async (handle, _) =>
        {
            await WaitFor(() => fake.Runs > 0, ct);

            var scheduled = (await handle.FetchHistoryAsync()).Events
                .Select(static e => e.ActivityTaskScheduledEventAttributes).Single(static a => a is not null && a.ActivityId.StartsWith("block:", StringComparison.Ordinal));
            Assert.True(scheduled.StartToCloseTimeout.ToTimeSpan() > TimeSpan.FromDays(1), "the old two-minute default must be gone");   // owner q93: the state timer bounds it
            Assert.Equal(0, fake.Cancelled);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task The_block_activity_is_attempted_once_and_a_thrown_Step_is_failed_with_its_type_and_message()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = new FakeBlockStep(static (_, _) => throw new InvalidOperationException("the judge fell over"));
        await Run(fake, maxDuration: null, async (handle, sindri) =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "route-evidence-desk");   // a retry would delay step.failed until it too had run, so one run here is one attempt

            Assert.Equal(1, fake.Runs);
            Assert.Equal("failed: InvalidOperationException: the judge fell over", (await FailedRow(sindri, ct)).Reason);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_Step_that_returns_Failed_is_step_failed_with_its_own_reason()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Failing("no provider answered");
        await Run(fake, maxDuration: null, async (handle, sindri) =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "route-evidence-desk");

            Assert.Equal("no provider answered", (await FailedRow(sindri, ct)).Reason);
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
            var stamp = new CommandStamp(CommandNonce.From(new string('c', 32)), new string('1', 64));
            var cancelled = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(new CancelRequested("human:cli", CancelOrigin.Command, stamp, Sequence: null, Parent: null)), new WorkflowUpdateOptions(new string('c', 32)));

            Assert.Equal("cancelled", cancelled.Instance.State);
            await WaitFor(() => fake.Cancelled == 1, ct);   // Temporal delivers the cancel in the answer to the Step's next heartbeat (the first goes out a second after it starts)
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
            Assert.Contains("wait for its result", first.InnerException!.Message, StringComparison.Ordinal);   // the fix names what really works
            Assert.DoesNotContain("takeover", first.InnerException.Message, StringComparison.Ordinal);
            Assert.Equal(0, fake.Cancelled);   // the engine's run goes on
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Takeover_during_a_block_run_cancels_it_and_a_complete_is_refused_until_a_handback_runs_it_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.HangingThenCompleting("ui");
        await Run(fake, maxDuration: null, async (handle, host) =>
        {
            var running = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "route-evidence");
            await WaitFor(() => fake.Runs > 0, ct);
            var takeover = new TakeoverRequested("human:cli", new CommandStamp(CommandNonce.From(new string('1', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), new WorkflowUpdateOptions(new string('1', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "taken-over");
            await WaitFor(() => fake.Cancelled == 1, ct);

            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", running.Run!.Value, OutcomeName.From("ui"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:decision")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            var refused = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('3', 32))).WaitAsync(TimeSpan.FromSeconds(20), ct));
            Assert.Contains("hand it back first: sindri workflow handback", refused.InnerException!.Message, StringComparison.Ordinal);   // a taken-over run answers nothing; only the handback moves it
            Assert.Contains("the handback runs the Step again", refused.InnerException.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("then complete run", refused.InnerException.Message, StringComparison.Ordinal);

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
        var env = server.Environment;
        await using var sindri = _harness.Sindri(_harness.Replacing);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, Route, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"T-{Interlocked.Increment(ref _items)}"), ct);
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

    // The state is visible a moment before its event is mirrored into the ledger, so the row is waited for.
    private static async Task<(string Reason, string? Via)> FailedRow(IServiceProvider sindri, CancellationToken ct)
    {
        (string?, string?)? row = null;
        await WaitFor(() =>
        {
            var rows = sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct).AsTask().GetAwaiter().GetResult().Value;
            row = rows.Select(static e => JsonDocument.Parse(e.Envelope.Payload))
                .Where(static p => p.RootElement.TryGetProperty("trigger", out var t) && t.GetString() == "step.failed")
                .Select(static p => ((string?, string?)?)(p.RootElement.GetProperty("detail").GetProperty("reason").GetString(), p.RootElement.TryGetProperty("via", out var via) ? via.GetString() : null))
                .FirstOrDefault();
            return row is not null;
        }, ct);
        return (row!.Value.Item1!, row.Value.Item2);
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
