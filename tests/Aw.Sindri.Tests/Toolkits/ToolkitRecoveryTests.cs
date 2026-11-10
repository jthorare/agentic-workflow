using Aw.Kernel.Testing;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Tests.BuildToolkit;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>After a serve restart no toolkit process exists, so the store's 'running' and 'paused' rows go back to 'built' (agent-first PR 16).</summary>
[Lane(TestLane.Unit)]
public sealed class ToolkitRecoveryTests
{
    [Fact]
    public async Task Running_and_paused_rows_become_built_and_every_other_row_stays()
    {
        var store = new ToolkitRig.FakeStore();
        foreach (var (name, state) in new[] { ("a", ToolkitState.Running), ("b", ToolkitState.Paused), ("c", ToolkitState.Built), ("d", ToolkitState.Failed), ("e", ToolkitState.Off) })
        {
            store.Items[name] = new Toolkit(name, "/tk/" + name, state, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, Manifest: null, CreatedBy: "human");
        }

        var changed = await new ToolkitRecovery(store).Run(TestContext.Current.CancellationToken);

        Assert.Equal(2, changed.Value);
        Assert.Equal<ToolkitState>(
            [ToolkitState.Built, ToolkitState.Built, ToolkitState.Built, ToolkitState.Failed, ToolkitState.Off],
            store.Items.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => p.Value.State));
        Assert.Equal("h", store.Items["a"].SourceHash);
    }
}
