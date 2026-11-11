using Aw.Kernel.Testing;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.ListToolkits;
using Aw.Sindri.Tests.BuildToolkit;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>toolkit_list shows what is true now (agent-first PR 16): the store row, corrected by which toolkits have a live process.</summary>
[Lane(TestLane.Unit)]
public sealed class ListToolkitsTests
{
    [Fact]
    public async Task A_row_the_store_calls_running_without_a_process_shows_built_and_a_failed_row_with_a_live_process_shows_running()
    {
        var store = new ToolkitRig.FakeStore();
        foreach (var (name, state) in new[] { ("a", ToolkitState.Running), ("b", ToolkitState.Failed), ("c", ToolkitState.Built) })
        {
            store.Items[name] = new Toolkit(name, "/tk/" + name, state, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, Manifest: null, CreatedBy: "human");
        }

        var listed = await new ListToolkitsService(store, new FakeRunningToolkits([FakeRunningToolkits.Make("b", ToolkitState.Running)])).Handle(new ListToolkitsRequest(), TestContext.Current.CancellationToken);

        Assert.Equal([("a", ToolkitState.Built), ("b", ToolkitState.Running), ("c", ToolkitState.Built)], listed.Value.Toolkits.Select(static t => (t.Name, t.State)));
    }

    [Fact]
    public async Task A_toolkit_with_a_live_paused_process_shows_paused_and_a_stale_paused_row_without_one_shows_built()
    {
        var store = new ToolkitRig.FakeStore();
        foreach (var name in new[] { "a", "b" })
        {
            store.Items[name] = new Toolkit(name, "/tk/" + name, ToolkitState.Paused, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, Manifest: null, CreatedBy: "human");
        }

        var listed = await new ListToolkitsService(store, new FakeRunningToolkits([FakeRunningToolkits.Make("a", ToolkitState.Paused)])).Handle(new ListToolkitsRequest(), TestContext.Current.CancellationToken);

        Assert.Equal([("a", ToolkitState.Paused), ("b", ToolkitState.Built)], listed.Value.Toolkits.Select(static t => (t.Name, t.State)));
    }
}
