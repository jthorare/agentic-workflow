using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The P3 eval cases through the real interpreter (engine spec "Phase 3": "from P4 it also runs them through the
/// interpreter on the time-skipping server (the two must agree)"; §5.1, §9.2). Each shipped 'workflow.step' case runs as
/// a 'RecipeWorkflow' execution, every trigger line sent as the Update it stands for; after each line the instance the
/// interpreter holds (state, park, outcome, open run) equals the one 'recipe eval' folds in process, and the history
/// replays clean.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InterpreterEvalTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string StepRecipe = "sindri/workflow.step.yaml";
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private const string EvalDirectory = "sindri/evals/workflow.step/";

    /// <summary>Every shipped 'workflow.step' case, each with its own item (T-11 up), so a case added later runs here too.</summary>
    public static TheoryData<string, string> Cases { get; } =
        [.. ShippedRecipes.Paths
            .Where(static p => p.StartsWith(EvalDirectory, StringComparison.Ordinal) && p.EndsWith(".jsonl", StringComparison.Ordinal))
            .Select(static (p, i) => (Path.GetFileNameWithoutExtension(p), "T-" + (11 + i).ToString(CultureInfo.InvariantCulture)))];

    [Theory(Timeout = 120_000)]
    [MemberData(nameof(Cases))]
    public async Task Each_workflow_step_eval_case_through_the_interpreter_agrees_with_recipe_eval(string name, string item)
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var evalCase = EvalCaseReader.Read(name, ShippedRecipes.Read($"{EvalDirectory}{name}.jsonl"), EvalComposition.Options).Case!; // why: a shipped case reads (P3's EvalRecipeTests)
        var started = EvalSimulation.Start(evalCase, EvalComposition.Recipe(StepRecipe), EvalComposition.Catalog, NoChildRecipes.Instance);
        Assert.False(started.IsError, started.IsError ? started.Error.Message : null);
        var simulation = started.Value;
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await Input(sindri, item, evalCase, ct));
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null);
            Assert.Equal(Shown(simulation), Seen(view));
            for (var i = 0; i < evalCase.Steps.Count; i++)
            {
                var line = evalCase.Steps[i];
                Assert.Equal(TriggerNames.StepCompleted, line.Trigger.Kind);
                var report = new StepCompleted("human:cli", view.Run!.Value, line.Trigger.Outcome!.Value, Outputs(line.Trigger), Tokens: 0, StepRuntime.Desk, Sha(name + ":" + i.ToString(CultureInfo.InvariantCulture))); // why: the view before a step.completed line has an open run (asserted through Seen)
                _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(Sha("nonce:" + name + ":" + i.ToString(CultureInfo.InvariantCulture))[..32])).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
                var folded = simulation.Step(line, i + 2);
                Assert.False(folded.IsError, folded.IsError ? folded.Error.Message : null);
                simulation = folded.Value.Next;
                Assert.Null(simulation.Diverges(line.Expect));

                view = await handle.QueryAsync(static wf => wf.GetInstance());
                Assert.Equal(Shown(simulation), Seen(view));
            }

            Assert.Null((await Replay(handle, ct)).ReplayFailure);
            if (view.Outcome is null)
            {
                await handle.TerminateAsync("test over");
            }
        }, ct);
    }

    /// <summary>The workflow input for the case's start line: its input facts stored as the eval stores them.</summary>
    private static async Task<InstanceStart> Input(IServiceProvider sindri, string item, EvalCase evalCase, CancellationToken ct)
    {
        var start = await WorkflowsHarness.StepStart(sindri, item, ct);
        var inputs = evalCase.Start.Start.Inputs.ToDictionary(static i => i.Key, static i => new InputFact("spec.md", EvalFacts.Value(i.Value)));
        return start with { Inputs = inputs };
    }

    private static Dictionary<FactKey, FactValue> Outputs(EvalTrigger trigger) =>
        (trigger.Outputs ?? new Dictionary<FactKey, System.Text.Json.JsonElement>()).ToDictionary(static o => o.Key, static o => EvalFacts.Value(o.Value));

    private static (string State, ParkReason? Park, string? Outcome, string? Run) Shown(EvalSimulation simulation) =>
        (simulation.Instance.State, simulation.Instance.Park, simulation.Instance.Outcome?.Value, simulation.Instance.Frame.Run?.Token.Value);

    private static (string State, ParkReason? Park, string? Outcome, string? Run) Seen(InstanceView view) =>
        (view.State, view.Question?.Reason, view.Outcome?.Value, view.Run?.Value);

    internal static async Task<WorkflowReplayResult> Replay(WorkflowHandle handle, CancellationToken ct) =>
        await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>())
            .ReplayWorkflowAsync(await handle.FetchHistoryAsync(), throwOnReplayFailure: false, ct);

    internal static string Sha(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
