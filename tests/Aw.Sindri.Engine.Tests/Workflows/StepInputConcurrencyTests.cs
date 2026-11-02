using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows.Inputs;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The Step inputs are filled together, not one after another (#1101): a Step with three inputs starts all three 'FillInput' activities before
/// any of them returns, the Step still receives them in declared order, and the injection row says how long each took. Every wait is bounded.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StepInputConcurrencyTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly TimeSpan _meet = TimeSpan.FromSeconds(4);

    private readonly WorkflowsHarness _harness = new();
    private readonly InputProbe _probe = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task All_inputs_of_a_Step_have_started_before_any_of_them_returns()
    {
        using var started = new CountdownEvent(3);
        var met = new System.Collections.Concurrent.ConcurrentQueue<bool>();
        _probe.Behavior = c =>
        {
            _ = started.Signal();
            met.Enqueue(started.Wait(_meet, TestContext.Current.CancellationToken));   // a sequential workflow starts the next fill only after this one returns: the wait runs out
            return new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { [c.Input.Value] = InputProbe.Planted });
        };

        await Run("T-1101a", async run =>
        {
            await WaitForRunStep(run.Handle);

            Assert.Equal(3, met.Count);
            Assert.All(met, static m => Assert.True(m));
        });
    }

    [Fact]
    public async Task The_Step_still_gets_its_inputs_in_declared_order()
    {
        using var started = new CountdownEvent(3);
        _probe.Behavior = c =>
        {
            _ = started.Signal();
            _ = started.Wait(_meet, TestContext.Current.CancellationToken);
            return new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { [c.Input.Value] = InputProbe.Planted });
        };

        await Run("T-1101b", async run =>
        {
            await WaitForRunStep(run.Handle);

            var rows = await run.Desk.Payloads("workflow.injection");
            var injection = Assert.Single(rows).GetProperty("detail").GetProperty("injection");

            Assert.Equal([ProbeInput.Id, SecondProbeInput.Id, ThirdProbeInput.Id], injection.EnumerateArray().Select(static e => e.GetProperty("input").GetString()), StringComparer.Ordinal);
        });
    }

    [Fact]
    public async Task The_injection_row_records_how_long_each_input_took()
    {
        await Run("T-1101c", async run =>
        {
            await WaitForRunStep(run.Handle);

            var rows = await run.Desk.Payloads("workflow.injection");
            var inputs = Assert.Single(rows).GetProperty("detail").GetProperty("inputs");

            Assert.Equal([ProbeInput.Id, SecondProbeInput.Id, ThirdProbeInput.Id], inputs.EnumerateArray().Select(static e => e.GetProperty("input").GetString()), StringComparer.Ordinal);
            Assert.All(inputs.EnumerateArray(), static e => Assert.True(e.GetProperty("durationMs").GetInt64() >= 0));
        });
    }

    private static async Task WaitForRunStep(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(30);
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
        services.AddSingleton(_probe).AddStepInput<ProbeInput>().AddStepInput<SecondProbeInput>().AddStepInput<ThirdProbeInput>();

    private async Task Run(string item, Func<Running, Task> test)
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.inputs", Recipe(ProbeInput.Id, SecondProbeInput.Id, ThirdProbeInput.Id), AddProbes);
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
