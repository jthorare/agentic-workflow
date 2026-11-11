using Aw.Kernel.Testing;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Tests.BuildToolkit;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>The serve start puts a 'running' row back to 'built' (agent-first PR 16): the supervisor starts empty, so the store must not claim otherwise.</summary>
[Lane(TestLane.Unit)]
public sealed class ToolkitRecoveryHostTests
{
    [Fact]
    public async Task The_host_resets_a_running_row_once_at_start_and_finishes()
    {
        var store = new ToolkitRig.FakeStore();
        store.Items["triage"] = new Toolkit("triage", "/tk/triage", ToolkitState.Running, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, Manifest: null, CreatedBy: "human");
        using var host = new ToolkitRecoveryHost(new ToolkitRecovery(store), TimeProvider.System, new ListLogger<ToolkitRecoveryHost>());

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.ExecuteTask!.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ToolkitState.Built, store.Items["triage"].State);
    }
}
