using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// A dev server that died under a live serve (review run 2, code-reviewer-r2-1): every Update and Signal on the instance
/// is SND-API-001 (Sindri is not running), not a bare RPC failure that surfaces as a 500, and the Kind (Unavailable) keeps
/// a fresh nonce claim so a retry with the same nonce resends the same Update id.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DeadTemporalTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-90/step/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('e', 32));

    [Fact(Timeout = 120_000)]
    public async Task Updates_and_signals_on_a_Temporal_that_died_are_SND_API_001()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        var environment = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        var settings = new TemporalSettings(home.Path, PinnedTemporal.Path());
        using var frontend = new TemporalFrontend(new DeadTemporalEndpoint(WorkflowsHarness.PortOf(environment)), settings);
        var runtime = new TemporalInstanceRuntime(frontend, new TemporalDeskReports(frontend), new TemporalCommands(frontend), settings, new KnownInstances());
        Assert.Equal("SND-WF-001", (await runtime.Get(_address, ct)).Error.Code);   // connected, and the instance isn't there
        await environment.ShutdownAsync();

        var command = new CommandFired("human:test", CommandVerb.From("ship"), new CommandStamp(_nonce, new string('d', 64)));
        var report = new StepCompleted("human:test", RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, FactValue>(), Tokens: 0, StepRuntime.Desk, new string('c', 64));
        var routed = new WaitSatisfied("service:workflow.events", new EventWaitSource(EventKind.From("item.routed"), 1), OutcomeName.From("startable"), new Dictionary<FactKey, FactValue>());

        var codes = new[]
        {
            (await runtime.Send(_address, _nonce, command, ct)).Error.Code,
            (await runtime.Send(_address, _nonce, report, ct)).Error.Code,
            (await runtime.Signal(_address, routed, ct)).Error.Code,
        };

        Assert.Equal(["SND-API-001", "SND-API-001", "SND-API-001"], codes);
    }
}
