using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows.Inputs;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The open run's input sources over a real Temporal dev server (plan S3): the 'GetInjection' Query answers the run, the state and one
/// source per filled input with the metadata the run recorded, and none of it holds the text. An address that is not an instance is SND-WF-001.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalInjectionSourcesTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();
    private readonly InputProbe _probe = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Read_answers_the_open_run_with_each_filled_input_and_never_its_text()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: Token);
        _harness.Recipe("workflow.inputs", Recipe, AddProbes);
        await using var sindri = _harness.Sindri(AddProbes, name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? env.Client.Options.Namespace : null);
        await WorkflowsHarness.HoldLease(sindri, Token);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.inputs", "T-81", Token));
            var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var sources = new TemporalInjectionSources(sindri.GetRequiredService<TemporalFrontend>());

            var read = await ReadOnceFilled(sources, InstanceAddress.From(handle.Id));

            Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
            Assert.Equal(("checks", view.Run!.Value.Value), (read.Value.State, read.Value.Run));
            var input = Assert.Single(read.Value.Inputs);
            Assert.Equal(ProbeInput.Id, input.Recorded.Input.Value);
            Assert.Equal(("step.checks", ProbeInput.Id), (input.Context.Step.Value, input.Context.Input.Value));
            Assert.DoesNotContain(InputProbe.Planted, JsonSerializer.Serialize(input.Recorded), StringComparison.Ordinal);

            var gone = await sources.Read(InstanceAddress.Root(ItemKey.From("T-999"), RecipeId.From("workflow.inputs"), 1), Token);
            Assert.True(gone.IsError);
            Assert.Equal("SND-WF-001", gone.Error.Code);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>The query answers an empty run until the step has filled its inputs, so this waits for the first fill; the last answer returns after 30 s.</summary>
    private static async Task<Result<InjectionSource>> ReadOnceFilled(TemporalInjectionSources sources, InstanceAddress address)
    {
        var read = await sources.Read(address, Token);
        var deadline = RealTime.NowDateTime.AddSeconds(30);
        while (read.IsSuccess && read.Value.Inputs.Count == 0 && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(50, Token);
            read = await sources.Read(address, Token);
        }

        return read;
    }

    private void AddProbes(IServiceCollection services) => services.AddSingleton(_probe).AddStepInput<ProbeInput>();

    private static string Recipe => string.Join('\n',
    [
        "schema: 1",
        "id: workflow.inputs",
        "version: 1",
        "inputs: [{ key: fact:item, type: item, kind: item }]",
        "initial: checks",
        "states:",
        "  checks: { kind: active, step: { use: step.checks, inputs: [" + ProbeInput.Id + "] }, timeout: { after: 30m, then: park }, next: \"Run the checks.\" }",
        "  done:   { kind: terminal, outcome: resolved }",
        "  broken: { kind: terminal, outcome: failed }",
        "transitions:",
        "  - { from: checks, to: done, on: step.completed, when: { outcome: passed } }",
        "  - { from: checks, to: broken, on: step.failed }",
    ]);
}
