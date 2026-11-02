using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows.Inputs;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The Step inputs at run time on the time-skipping server (plan S3): one 'FillInput' activity per input before the Step opens,
/// metadata only in history, a failing input fails the Step, and an input whose runtime is gone after approval parks the
/// instance with the 'no runtime' park, which a restart tries again.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StepInputWorkflowTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();
    private readonly InputProbe _probe = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Fill_runs_once_per_input_before_RunStep_with_activity_id_inputs_token_n()
    {
        await Run("T-71", [ProbeInput.Id, SecondProbeInput.Id], async run =>
        {
            var task = await run.Desk.Until(static v => v.Task is not null);
            await WaitForRunStep(run.Handle);

            var ids = (await run.Handle.FetchHistoryAsync()).Events.Where(static e => e.ActivityTaskScheduledEventAttributes is not null)
                .Select(static e => e.ActivityTaskScheduledEventAttributes.ActivityId).Where(static id => id.StartsWith("inputs:", StringComparison.Ordinal) || id.StartsWith("run:", StringComparison.Ordinal)).ToList();

            Assert.Equal([$"inputs:{task.Run!.Value.Value}:1", $"inputs:{task.Run.Value.Value}:2", $"run:{task.Run.Value.Value}:1"], ids, StringComparer.Ordinal);
            Assert.Equal([$"{ProbeInput.Id} step.checks", $"{SecondProbeInput.Id} step.checks"], _probe.Calls.Order(StringComparer.Ordinal), StringComparer.Ordinal);   // the fills run together: each once, in no fixed order
        });
    }

    [Fact]
    public async Task Fill_result_in_history_holds_metadata_and_no_text()
    {
        await Run("T-72", [ProbeInput.Id], async run =>
        {
            _ = await run.Desk.Until(static v => v.Task is not null);
            await WaitForRunStep(run.Handle);

            var payloads = Payloads((await run.Handle.FetchHistoryAsync()).ToJson());

            Assert.DoesNotContain(InputProbe.Planted, payloads, StringComparison.Ordinal);
            Assert.Contains(StepInputRunner.Sha(InputProbe.Planted), payloads, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Throwing_fill_after_retries_is_step_failed_and_routes_on_step_failed()
    {
        _probe.Behavior = null;
        await Run("T-73", [ProbeInput.Id], async run =>
        {
            var result = await run.Handle.GetResultAsync();

            Assert.Equal("broken", result.State);
            Assert.Equal(3, _probe.Calls.Count);
            Assert.DoesNotContain("told to throw", (await run.Handle.FetchHistoryAsync()).ToJson(), StringComparison.Ordinal);   // the message could carry text: only the type is kept
        });
    }

    [Fact]
    public async Task Invalid_section_name_is_step_failed_SND_WF_019_without_retries()
    {
        _probe.Behavior = static _ => new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["Bad Name"] = "x" });
        await Run("T-74", [ProbeInput.Id], async run =>
        {
            var result = await run.Handle.GetResultAsync();

            Assert.Equal("broken", result.State);
            _ = Assert.Single(_probe.Calls);
        });
    }

    [Fact]
    public async Task Runtime_gone_after_approval_parks_NoRuntime_and_a_restart_parks_again()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.inputs", Recipe(ProbeInput.Id), AddProbes);   // approved on a build that had the runtime
        await using var sindri = _harness.Sindri(static s => s.AddBlockDescriptor(ProbeInput.Descriptor));   // the build it runs on has the descriptor only
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(server.Environment.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.inputs", "T-69", ct));
            var desk = new DeskInstance(sindri, handle);
            var parked = await desk.Until(static v => v.Question is not null);
            var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('4', 32)), new string('0', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('4', 32)));
            var again = await desk.Until(v => v.Question is not null && v.Run != parked.Run);

            Assert.Equal(ParkReason.NoRuntime, parked.Question!.Reason);
            Assert.Equal($"input {ProbeInput.Id} of step.checks has no runtime in this build (SND-WF-016; fix: install the block pack or rebuild, then restart)", parked.Question.Question);
            Assert.Null(parked.Task);
            Assert.Equal((ParkReason.NoRuntime, parked.Question.Question), (again.Question!.Reason, again.Question.Question));
            Assert.Empty(_probe.Calls);
            Assert.DoesNotContain((await handle.FetchHistoryAsync()).Events, static e => e.EventType == EventType.ActivityTaskScheduled && e.ActivityTaskScheduledEventAttributes.ActivityType.Name == "RunStep");
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task History_with_fills_replays_without_calling_Fill()
    {
        await Run("T-75", [ProbeInput.Id], async run =>
        {
            _ = await run.Desk.Until(static v => v.Task is not null);
            await WaitForRunStep(run.Handle);   // the task is open before the FillInput activity has run; counting before it lets the fill land during the replay
            var calls = _probe.Calls.Count;

            var replay = await InterpreterEvalTests.Replay(run.Handle, TestContext.Current.CancellationToken);

            Assert.Null(replay.ReplayFailure);
            Assert.Equal(calls, _probe.Calls.Count);
        });
    }

    [Fact]
    public async Task Texts_are_kept_while_the_Step_is_open_and_dropped_when_the_Step_is_reported()
    {
        await Run("T-76", [ProbeInput.Id], async run =>
        {
            var task = await run.Desk.Until(static v => v.Task is not null);
            await WaitForRunStep(run.Handle);   // the task is open before the FillInput activity has kept its text; RunStep is scheduled only after
            var texts = run.Desk.Sindri.GetRequiredService<IInjectionTexts>();
            var key = new InjectionKey(run.Handle.Id, "checks", task.Run!.Value.Value, 1);

            Assert.Equal(InputProbe.Planted, texts.Get(key)!.Sections["notes"]);
            var report = new StepCompleted("human:cli", task.Run!.Value, OutcomeName.From("passed"), new Dictionary<FactKey, FactValue>(), Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await run.Handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
            _ = await run.Handle.GetResultAsync();

            Assert.Null(texts.Get(key));
        });
    }

    [Fact]
    public async Task A_cancelled_instance_drops_its_texts()
    {
        await Run("T-77", [ProbeInput.Id], async run =>
        {
            var task = await run.Desk.Until(static v => v.Task is not null);
            await WaitForRunStep(run.Handle);   // the task is open before the FillInput activity has kept its text; RunStep is scheduled only after
            var texts = run.Desk.Sindri.GetRequiredService<IInjectionTexts>();
            var key = new InjectionKey(run.Handle.Id, "checks", task.Run!.Value.Value, 1);
            Assert.NotNull(texts.Get(key));

            await run.Handle.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<Exception>(() => run.Handle.GetResultAsync());

            Assert.Null(texts.Get(key));
        });
    }

    [Fact]
    public async Task A_later_input_that_fails_drops_the_texts_the_earlier_inputs_filled()
    {
        _probe.Behavior = static c => string.Equals(c.Input.Value, SecondProbeInput.Id, StringComparison.Ordinal)
            ? throw new InvalidOperationException("second fails")
            : new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = InputProbe.Planted });
        await Run("T-78", [ProbeInput.Id, SecondProbeInput.Id], async run =>
        {
            var result = await run.Handle.GetResultAsync();
            var texts = run.Desk.Sindri.GetRequiredService<IInjectionTexts>();

            Assert.Equal("broken", result.State);
            Assert.Null(texts.Get(new InjectionKey(run.Handle.Id, "checks", "1.1", 1)));
        });
    }

    [Fact]
    public async Task Inputs_that_together_pass_the_old_plan_cap_fill_and_run_the_Step()
    {
        var big = new string('x', 200 * 1024);   // 3 sections of 200 KiB: 600 KiB an input, 1.2 MiB together, past the 1 MiB plan cap a new run no longer keeps
        _probe.Behavior = _ => new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = big, ["b"] = big, ["c"] = big });
        await Run("T-79", [ProbeInput.Id, SecondProbeInput.Id], async run =>
        {
            var task = await run.Desk.Until(static v => v.Task is not null);
            await WaitForRunStep(run.Handle);
            var texts = run.Desk.Sindri.GetRequiredService<IInjectionTexts>();

            Assert.Equal(2, _probe.Calls.Count);
            Assert.NotNull(texts.Get(new InjectionKey(run.Handle.Id, "checks", task.Run!.Value.Value, 1)));
            Assert.NotNull(texts.Get(new InjectionKey(run.Handle.Id, "checks", task.Run.Value.Value, 2)));
        });
    }

    [Fact]
    public async Task One_injection_row_per_run_holds_the_plan_metadata_and_no_text_anywhere_in_the_ledger()
    {
        await Run("T-79", [ProbeInput.Id, SecondProbeInput.Id], async run =>
        {
            _ = await run.Desk.Until(static v => v.Task is not null);
            await WaitForRunStep(run.Handle);

            var rows = await run.Desk.Payloads("workflow.injection");
            var injection = Assert.Single(rows).GetProperty("detail").GetProperty("injection");

            Assert.Equal(["notes"], injection.EnumerateArray().Select(static e => e.GetProperty("section").GetString()).Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
            Assert.Equal(StepInputRunner.Sha(InputProbe.Planted), injection[0].GetProperty("sha256").GetString());
            var ledger = await run.Desk.Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(ledger.Value, static e => e.Envelope.Payload.Contains(InputProbe.Planted, StringComparison.Ordinal));
        });
    }

    /// <summary>The history's payloads decoded (Temporal stores them as base64), joined: what a grep of the stored history would see.</summary>
    private static string Payloads(string historyJson) => string.Join('\n', System.Text.RegularExpressions.Regex.Matches(historyJson, "\"data\":\\s*\"(?<b64>[^\"]+)\"", System.Text.RegularExpressions.RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(5))
        .Select(static m => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups["b64"].Value))));

    /// <summary>Waits until 'RunStep' is scheduled: the inputs have filled and the desk task is recorded.</summary>
    private static async Task WaitForRunStep(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        while (RealTime.NowDateTime < deadline)
        {
            if ((await handle.FetchHistoryAsync()).Events.Any(static e => e.ActivityTaskScheduledEventAttributes?.ActivityType.Name == "RunStep"))
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail("RunStep was never scheduled");
    }

    private void AddProbes(IServiceCollection services) =>
        services.AddSingleton(_probe).AddStepInput<ProbeInput>().AddStepInput<SecondProbeInput>();

    /// <summary>Starts the recipe with <paramref name="inputs"/> on a worker whose build has their runtimes, runs <paramref name="test"/>, and ends the instance.</summary>
    private async Task Run(string item, string[] inputs, Func<Running, Task> test)
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.inputs", Recipe(inputs), AddProbes);
        await using var sindri = _harness.Sindri(AddProbes);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(server.Environment.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.inputs", item, ct));
            await test(new Running(handle, new DeskInstance(sindri, handle)));
            if ((await handle.DescribeAsync()).Status == WorkflowExecutionStatus.Running)
            {
                await handle.TerminateAsync("test over");
            }
        }, ct);
    }

    private static string Recipe(params string[] inputs) => string.Join('\n',
    [
        "schema: 1",
        "id: workflow.inputs",
        "version: 1",
        "inputs: [{ key: fact:item, type: item, kind: item }]",
        "initial: checks",
        "states:",
        "  checks: { kind: active, step: { use: step.checks, inputs: [" + string.Join(", ", inputs) + "] }, timeout: { after: 30m, then: park }, next: \"Run the checks.\" }",
        "  done:   { kind: terminal, outcome: resolved }",
        "  broken: { kind: terminal, outcome: failed }",
        "transitions:",
        "  - { from: checks, to: done, on: step.completed, when: { outcome: passed } }",
        "  - { from: checks, to: broken, on: step.failed }",
    ]);

    private sealed record Running(WorkflowHandle<RecipeWorkflow, RecipeResult> Handle, DeskInstance Desk);
}
