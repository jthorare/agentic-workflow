using Aw.Kernel.Testing;
using Aw.Sindri.Features.ListToolkitCalls;
using Aw.Sindri.Testing.Toolkits;

namespace Aw.Sindri.Tests.Toolkits;

[Lane(TestLane.Unit)]
public sealed class ListToolkitCallsTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Calls_come_newest_first_for_one_toolkit_up_to_the_limit()
    {
        var store = new InMemoryToolkitCalls();
        _ = await store.Add(ToolkitCallsContract.Sample("notes", "a", 1), Cancel);
        _ = await store.Add(ToolkitCallsContract.Sample("other", "x", 2), Cancel);
        _ = await store.Add(ToolkitCallsContract.Sample("notes", "b", 3), Cancel);
        _ = await store.Add(ToolkitCallsContract.Sample("notes", "c", 4), Cancel);

        var answer = await new ListToolkitCallsService(store).Handle(new ListToolkitCallsRequest("notes", Limit: 2), Cancel);

        Assert.True(answer.IsSuccess);
        Assert.Equal(["c", "b"], answer.Value.Calls.Select(static c => c.Tool), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_limit_below_one_is_clamped_to_one()
    {
        var store = new InMemoryToolkitCalls();
        _ = await store.Add(ToolkitCallsContract.Sample("notes", "a", 1), Cancel);
        _ = await store.Add(ToolkitCallsContract.Sample("notes", "b", 2), Cancel);

        var zero = await new ListToolkitCallsService(store).Handle(new ListToolkitCallsRequest(Toolkit: null, Limit: 0), Cancel);

        Assert.Equal(["b"], zero.Value.Calls.Select(static c => c.Tool), StringComparer.Ordinal);
    }
}
