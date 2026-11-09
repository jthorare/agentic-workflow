using System.Text.Json.Nodes;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>sindri_handoff (#2258 PR 12): the arguments it binds, and a call through the MCP endpoint that makes one item.</summary>
public sealed class SindriHandoffToolTests(SharedMcpWorld shared) : IClassFixture<SharedMcpWorld>
{
    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_call_makes_an_item_and_the_same_offer_again_returns_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);
        var arguments = new JsonObject { ["title"] = "Fix the login timeout", ["offerKey"] = "mcp-offer-1", ["prompt"] = "Run the suite twice." }.ToJsonString();

        var first = await mcp.Call("sindri_handoff", arguments, ct);
        var second = await mcp.Call("sindri_handoff", arguments, ct);

        Assert.NotEqual(true, first.IsError);
        Assert.NotEqual(true, second.IsError);
        var item = first.StructuredContent!.Value.GetProperty("itemId").GetString();
        Assert.Equal(item, second.StructuredContent!.Value.GetProperty("itemId").GetString());
        Assert.StartsWith($"handed off as {item} (offer mcp-offer-1)", string.Concat(first.Content.OfType<TextContentBlock>().Select(b => b.Text)), StringComparison.Ordinal);
        var scan = await world.Host.App.Services.GetRequiredService<ILedgerReader>()
            .Scan(0, 500, new LedgerFilter(TypeFilter.Parse(EventTypes.ItemHandoff).Value, Item: item), ct);
        var recorded = Assert.Single(scan.Value.Events);
        Assert.Equal("handoff:mcp", recorded.Envelope.Actor);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_call_with_an_invalid_source_is_an_error_result()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mcp = await McpSession.Open(shared.World.Host, "alpha", ct);

        var result = await mcp.Call("sindri_handoff", """{"title":"Fix it","source":"Not Valid"}""", ct);

        Assert.True(result.IsError);
    }
}
