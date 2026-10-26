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

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What the heavy-slot tests share (LN-WF3): a home whose host composes the real directory lock at a temp path with one slot, a fake judge block, and the heavy recipe.</summary>
internal sealed class HeavyRig : IDisposable
{
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");
    private static int _items = 100;
    private readonly DirectoryInfo _state;

    public HeavyRig()
    {
        _state = Directory.CreateTempSubdirectory("aw-heavy-wf-");
        Lock = Path.Combine(_state.FullName, "heavy-job.lock");
        StateRoot = Directory.CreateTempSubdirectory("aw-heavy-state-").FullName;
    }

    /// <summary>The lock's slot 1: with one slot, it exists while a slot is held.</summary>
    public string Lock { get; }

    /// <summary>The Sindri's own state root (AW_STATE_DIR), so the box's heavy-slots-cap file never decides the slot count (#1173).</summary>
    public string StateRoot { get; }

    public WorkflowsHarness Harness { get; } = new();

    public void Dispose()
    {
        Harness.Dispose();
        _state.Delete(recursive: true);
        Directory.Delete(StateRoot, recursive: true);
    }

    /// <summary>The host with <paramref name="fake"/> as the judge block, the heavy recipe approved and the lease held.</summary>
    public async Task<ServiceProvider> Compose(FakeBlockStep fake)
    {
        Harness.Replacing = services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton(new BlockStepRuntimes([new BlockStepRegistration(_judge)])));
            _ = services.AddKeyedSingleton<IBlockStep>(_judge.Value, fake);
        };
        Harness.Recipe(HeavyRecipes.Id, HeavyRecipes.Heavy());
        var sindri = Harness.Sindri(Harness.Replacing, name => name switch { "AW_HEAVY_JOB_LOCK" => Lock, "AW_HEAVY_SLOTS" => "1", "AW_STATE_DIR" => StateRoot, _ => null });
        await WorkflowsHarness.HoldLease(sindri, TestContext.Current.CancellationToken);
        return sindri;
    }

    /// <summary>Starts an instance of the heavy recipe on a fresh item.</summary>
    public async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start(IServiceProvider sindri, Temporalio.Testing.WorkflowEnvironment environment)
    {
        var ct = TestContext.Current.CancellationToken;
        var start = await WorkflowsHarness.RecipeStart(sindri, HeavyRecipes.Id, Next(), ct) with
        {
            Inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:change")] = new InputFact("change.txt", Harness.Fact("Add a button")),
                [FactKey.From("fact:diff")] = new InputFact("diff.txt", Harness.Fact("https://example.test/pr/1")),
            },
        };
        return await WorkflowsHarness.Start(environment.Client, start);
    }

    public static StepCompleted DeskReport(RunToken run) => new(
        "human:cli", run, OutcomeName.From("done"),
        new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null) },
        Tokens: 0, StepRuntime.Desk, new string('f', 64));

    public static BlockStepResult FakeResult() => BlockStepResult.Completed(
        OutcomeName.From("ui"),
        new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:decision")] = JsonSerializer.SerializeToElement(new { choice = "ui", provider = "fake", decisionId = "JD-1" }) });

    /// <summary>The 'workflow.heavy' rows' triggers, in ledger order.</summary>
    public static async Task<IReadOnlyList<string>> Rows(IServiceProvider sindri, CancellationToken ct)
    {
        var read = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, ct);
        Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
        return [.. read.Value.Where(static e => e.Envelope.Type == "workflow.heavy").Select(static e => JsonDocument.Parse(e.Envelope.Payload).RootElement.GetProperty("trigger").GetString()!)];
    }

    public static async Task Eventually(Func<bool> condition, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(30);
        while (!condition() && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(condition());
    }

    private static string Next() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"T-{Interlocked.Increment(ref _items)}");
}
