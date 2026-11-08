using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>A command or report on a dead Temporal names the nonce to rerun with; a start does not (a class of its own so the dead-server tests run in parallel and stay inside the integration lane budget).</summary>
[Lane(TestLane.Integration)]
public sealed class DeadTemporalNonceTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-90/step/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('e', 32));

    [Fact(Timeout = 120_000)]
    public async Task A_command_or_report_on_a_Temporal_that_died_names_the_nonce_to_rerun_with_and_a_start_does_not()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        var settings = new TemporalSettings(home.Path, PinnedTemporal.Path());
        using var frontend = new TemporalFrontend(new DeadTemporalEndpoint(WorkflowsHarness.PortOf(environment)), settings);
        var runtime = new TemporalInstanceRuntime(frontend, new TemporalDeskReports(frontend), new TemporalCommands(frontend), settings, new KnownInstances());
        Assert.Equal("SND-WF-001", (await runtime.Get(_address, ct)).Error.Code);
        await environment.ShutdownAsync();

        var command = new CommandFired("human:test", CommandVerb.From("ship"), new CommandStamp(_nonce, new string('d', 64)));
        var report = new StepCompleted("human:test", RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, FactValue>(), Tokens: 0, StepRuntime.Desk, new string('c', 64));
        var routed = new WaitSatisfied("service:workflow.events", new EventWaitSource(EventKind.From("item.routed"), 1), OutcomeName.From("startable"), new Dictionary<FactKey, FactValue>());

        var sent = (await runtime.Send(_address, _nonce, command, ct)).Error.Message;
        var completed = (await runtime.Send(_address, _nonce, report, ct)).Error.Message;
        var signalled = (await runtime.Signal(_address, routed, ct)).Error.Message;

        Assert.All([sent, completed], m => Assert.Contains($"--nonce {_nonce.Value}", m, StringComparison.Ordinal));
        Assert.All([sent, completed], m => Assert.Contains("sindri workflow status", m, StringComparison.Ordinal));
        Assert.All([sent, completed, signalled], m => Assert.DoesNotContain("start sindri serve", m, StringComparison.Ordinal));
        Assert.All([sent, completed, signalled], m => Assert.Contains("sindri doctor", m, StringComparison.Ordinal));
        Assert.DoesNotContain("--nonce", signalled, StringComparison.Ordinal);
    }
}
