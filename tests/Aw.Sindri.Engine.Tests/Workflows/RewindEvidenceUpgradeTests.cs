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
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Re-running a parked run's check after the check was fixed (#1270, ask 3), on the owner's case in the shape of workflow.scoping: the evidence check
/// reports 'incomplete' with the same findings twice, so entering draft again parks the run no-progress. The build then gets a fixed check; 'rewind --to evidence'
/// runs it again on the facts the run holds, without drafting, sizing or proposing again, and the run reaches the stop.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RewindEvidenceUpgradeTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly BlockId _evidence = BlockId.From("step.test.evidence");

    private static readonly BlockDescriptor _descriptor = new(
        _evidence, BlockKind.Step, Version: 1, [FactKey.From("fact:output")], [new OutputSpec(FactKey.From("fact:findings"), [OutcomeName.From("incomplete")])],
        [OutcomeName.From("complete"), OutcomeName.From("incomplete")], Capability.None, [], Rung: null, DeskCapable: false, Runtime: BlockRuntime.Block);

    private static readonly string[] _passes = ["draft", "size", "propose", "draft", "size", "propose"];   // the check is incomplete after each propose, the second time with the same findings
    private static readonly string[] _findings = ["SCP-ACC-003: A1 names an id the facts do not hold"];

    private readonly WorkflowsHarness _harness = new();
    private volatile bool _upgraded;

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 180_000)]
    public async Task A_run_parked_on_its_evidence_check_reruns_evidence_on_its_stored_facts_after_an_upgrade_and_reaches_the_stop()
    {
        var ct = TestContext.Current.CancellationToken;
        var check = new FakeBlockStep((_, _) => ValueTask.FromResult(_upgraded
            ? BlockStepResult.Completed(OutcomeName.From("complete"), new Dictionary<FactKey, JsonElement>())
            : BlockStepResult.Completed(OutcomeName.From("incomplete"), new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:findings")] = JsonSerializer.SerializeToElement(_findings) })));
        _harness.Replacing = services => Register(services, check);
        _harness.Recipe("workflow.test-rewind-evidence", await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Workflows", "Fixtures", "workflow.test-rewind-evidence.yaml"), ct));
        await using var sindri = _harness.Sindri(_harness.Replacing);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.test-rewind-evidence", "T-1270", ct);
            var handle = await WorkflowsHarness.Start(server.Environment.Client, start with { Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:request")] = new InputFact("request.txt", _harness.Fact("Add a choice")) } });
            var runs = new List<RunToken>();
            foreach (var state in _passes)
            {
                await Report(handle, state, runs, _harness.Fact("the drafted scope"));
            }

            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            Assert.Equal(2, check.Runs);
            Assert.Contains("--to evidence", parked.Next, StringComparison.Ordinal);
            Assert.Contains(parked.AllowedCommands, c => string.Equals(c.Action, "ApplyCommand:rewind", StringComparison.Ordinal) && c.Cli.EndsWith("--to evidence", StringComparison.Ordinal));

            _upgraded = true;   // the build now ships a check that passes on these facts
            _ = await handle.ExecuteUpdateAsync(wf => wf.RewindAsync(new RewindRequested("human:cli", Stamp(), "evidence")), new WorkflowUpdateOptions(RealTime.NewGuid().ToString("N")));
            var stop = await WorkflowsHarness.Until(handle, static v => v.State == "stop" && v.Run is not null);

            Assert.Equal(3, check.Runs);   // the check ran once more
            Assert.Equal(parked.FactShas, stop.FactShas);   // every fact is as it was
            await WaitForTransitions(sindri, ct);
            var to = await Transitions(sindri, ct);
            Assert.Equal((2, 2), (to.Count(static t => t == "size"), to.Count(static t => t == "propose")));   // nothing was drafted, sized or proposed again
            Assert.Equal("stop", to[^1]);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    private static async Task Report(WorkflowHandle<RecipeWorkflow, RecipeResult> handle, string state, List<RunToken> runs, FactValue output)
    {
        var open = await WorkflowsHarness.Until(handle, v => v.State == state && v.Run is { } run && !runs.Contains(run));
        runs.Add(open.Run!.Value);
        var report = new StepCompleted("human:cli", open.Run.Value, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
        _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(RealTime.NewGuid().ToString("N")));
    }

    /// <summary>The state each mirrored transition went to, in ledger order.</summary>
    private static async Task<List<string>> Transitions(IServiceProvider sindri, CancellationToken ct)
    {
        var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value;
        return [.. rows.Where(static e => e.Envelope.Type == "workflow.transition").Select(static e => JsonDocument.Parse(e.Envelope.Payload)).Where(static p => p.RootElement.TryGetProperty("to", out _)).Select(static p => p.RootElement.GetProperty("to").GetString()!)];
    }

    // The state is visible a moment before its event is mirrored into the ledger.
    private static async Task WaitForTransitions(IServiceProvider sindri, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(20);
        while (RealTime.NowDateTime < deadline && (await Transitions(sindri, ct)) is not [.., "stop"])
        {
            await Task.Delay(50, ct);
        }
    }

    private static void Register(IServiceCollection services, FakeBlockStep check)
    {
        _ = services.Replace(ServiceDescriptor.Singleton(new BlockStepRuntimes([new BlockStepRegistration(_evidence)])));
        _ = services.AddKeyedSingleton<IBlockStep>(_evidence.Value, check);
        _ = services.Replace(ServiceDescriptor.Singleton(provider => new BlockCatalog([.. provider.GetServices<BlockDescriptor>(), _descriptor], provider.GetServices<EventDescriptor>())));
    }

    private static CommandStamp Stamp() => new(CommandNonce.From(RealTime.NewGuid().ToString("N")), new string('0', 64));
}
