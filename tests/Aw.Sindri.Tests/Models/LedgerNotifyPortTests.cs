using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.Models;

/// <summary>A notify node's note becomes one 'workflow.notify' event; posting the same note again adds nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class LedgerNotifyPortTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");
    private static readonly NodeId _node = NodeId.From("notify");

    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LedgerNotifyPort Port() => new(_h.Store, _h.Held, _h.Events, _h.Callers);

    [Fact]
    public async Task A_post_appends_one_event_with_the_run_node_title_and_body()
    {
        _ = await _h.HoldLease();

        var posted = await Port().Post(_run, _node, "Drifted", "2 contracts changed", Token);

        Assert.True(posted.IsSuccess);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(EventTypes.WorkflowNotify, recorded.Envelope.Type);
        var payload = JsonNode.Parse(recorded.Envelope.Payload)!;
        Assert.Equal("nightly@3:12", payload["run"]!.GetValue<string>());
        Assert.Equal("notify", payload["node"]!.GetValue<string>());
        Assert.Equal("Drifted", payload["title"]!.GetValue<string>());
        Assert.Equal("2 contracts changed", payload["body"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_second_identical_post_adds_nothing()
    {
        _ = await _h.HoldLease();
        var port = Port();
        _ = await port.Post(_run, _node, "Drifted", "2 contracts changed", Token);

        var again = await port.Post(_run, _node, "Drifted", "2 contracts changed", Token);

        Assert.True(again.IsSuccess);
        _ = Assert.Single(_h.Store.Events);
    }

    [Fact]
    public async Task A_post_without_the_lease_is_refused_and_appends_nothing()
    {
        var posted = await Port().Post(_run, _node, "Drifted", body: null, Token);

        Assert.True(posted.IsError);
        Assert.Empty(_h.Store.Events);
    }
}
