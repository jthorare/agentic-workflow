using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Engine;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>Reads, a start and a report on a dead Temporal are SND-API-001 and a fresh nonce claim is released (a class of its own, see <see cref="DeadTemporalNonceTests"/>).</summary>
[Lane(TestLane.Integration)]
public sealed class DeadTemporalReadsTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-90/step/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('e', 32));

    [Fact(Timeout = 120_000)]
    public async Task Reads_start_and_a_report_on_a_Temporal_that_died_are_SND_API_001_and_a_fresh_claim_is_released()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        var settings = new TemporalSettings(home.Path, PinnedTemporal.Path());
        using var frontend = new TemporalFrontend(new DeadTemporalEndpoint(WorkflowsHarness.PortOf(environment)), settings);
        var known = new KnownInstances();
        var runtime = new TemporalInstanceRuntime(frontend, new TemporalDeskReports(frontend), new TemporalCommands(frontend), settings, known);
        var directory = new TemporalInstanceDirectory(frontend, known);
        var like = new TemporalLikeRuntime();   // why: the approval check and the caller, which are not Temporal's
        var nonces = new InMemoryCommandNonces(new FakeTimeProvider());
        var service = new CompleteStepService(directory, runtime, like, new InMemoryFactStore(), nonces, like, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(like, new FakeLauncher())));
        Assert.Equal("SND-WF-001", (await runtime.Get(_address, ct)).Error.Code);
        await environment.ShutdownAsync();

        var request = new CompleteStepRequest(_address.Value, RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);
        var reported = await service.Handle(request, ct);
        var released = await nonces.Claim(_nonce, "another command", ct);   // fresh again: the report sent nothing
        var start = new InstanceStart(new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64))), ItemKey.From("T-90"), Attempt: 1, new Dictionary<FactKey, InputFact>(), Tenant: "local", Ring: 0);

        Assert.Equal("SND-API-001", reported.Error.Code);
        Assert.Equal(NonceClaim.Fresh, released.Value);
        Assert.Equal("SND-API-001", (await runtime.Start(start, ct)).Error.Code);
        Assert.Equal("SND-API-001", (await directory.Latest(ItemKey.From("T-90"), RecipeId.From("workflow.step"), ct)).Error.Code);
        Assert.Equal("SND-API-001", (await directory.Resolve("T-90", ct)).Error.Code);
        Assert.Equal("SND-API-001", (await directory.Resolve(WorkflowInstanceId.Of(InstanceAddress.From("T-91/step/1")).Value, ct)).Error.Code);
    }
}
