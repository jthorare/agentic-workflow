using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// <see cref="IInstanceLiveness"/> over the real Temporal client (engine P6 unit (d)): the same contract the fake
/// honours, against the time-skipping server. An instance runs from its start (no worker is needed) and ends when it is
/// terminated.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalInstanceLivenessContractTests(SindriTimeSkippingServer server) : InstanceLivenessContract, IClassFixture<SindriTimeSkippingServer>, IAsyncLifetime
{
    private readonly WorkflowsHarness _harness = new();
    private ServiceProvider? _sindri;
    private WorkflowHandle? _handle;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_sindri is not null)
        {
            await _sindri.DisposeAsync();
        }

        _harness.Dispose();
    }

    protected override async ValueTask<IInstanceLiveness> Create()
    {
        var space = server.Environment.Client.Options.Namespace;
        _sindri = _harness.Sindri(static _ => { }, name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? space : null);   // the server's namespace, as the bridge needs it
        await WorkflowsHarness.HoldLease(_sindri, Token);
        _ = await WorkflowsHarness.AsServe(_sindri, server.Environment, Token);
        return _sindri.GetRequiredService<IInstanceLiveness>();
    }

    protected override async Task Run()
    {
        var start = await WorkflowsHarness.StepStart(_sindri!, ItemOf(Address), Token);
        _handle = await WorkflowsHarness.Start(server.Environment.Client, start);
    }

    private static string ItemOf(Core.Workflow.Engine.InstanceAddress address) => address.Value.Split('/')[0];

    protected override async Task End() => await _handle!.TerminateAsync("test over");
}
