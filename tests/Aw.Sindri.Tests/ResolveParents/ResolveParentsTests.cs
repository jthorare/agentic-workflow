using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Features.ResolveParents;
using Aw.Sindri.Testing.Line;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.ResolveParents;

/// <summary>LN-PKT6: before a reader reads a packet, each hash in its parents resolves to a frozen packet, or the read is a mismatch (SND-LINE-001).</summary>
public sealed class ResolveParentsTests
{
    private const string Design = "sha256:a034b1f35f8ca257d23db5b1bef971adaecd161565a7962c0445c271917ef05c";
    private const string Scope = "sha256:9304bedd0b22e5874e786950f80e8c7660f749f4a328d422bf2b446c742fed24";

    private static ResolveParentsService Service(params FrozenPacket[] held) => new(new InMemoryFrozenPackets(held));

    private static readonly FrozenPacket _designRecord = new(Design, "design-expiry", "design", PacketFreezer.Owner);
    private static readonly FrozenPacket _scopeRecord = new(Scope, "scope-expiry", "scoping", PacketFreezer.Machine);

    [Fact, Trait("Lane", "unit")]
    public async Task Parents_that_are_all_frozen_resolve_in_the_order_listed()
    {
        var result = await Service(_scopeRecord, _designRecord).Handle(new ResolveParentsRequest([Design, Scope]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([_designRecord, _scopeRecord], result.Value.Frozen);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_packet_with_no_parents_resolves_to_nothing()
    {
        var result = await Service().Handle(new ResolveParentsRequest([]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Frozen);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_parent_nobody_froze_is_SND_LINE_001_naming_the_hash()
    {
        var result = await Service(_scopeRecord).Handle(new ResolveParentsRequest([Scope, Design]), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("SND-LINE-001", result.Error.Code);
        Assert.Contains(Design, result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Scope, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_unresolved_parent_is_named()
    {
        var result = await Service().Handle(new ResolveParentsRequest([Scope, Design]), CancellationToken.None);

        Assert.Equal("SND-LINE-001", result.Error.Code);
        Assert.Contains(Scope, result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(Design, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_parent_that_is_no_hash_is_SND_LINE_001_and_never_reaches_the_records()
    {
        var result = await Service(_scopeRecord).Handle(new ResolveParentsRequest(["../../escape"]), CancellationToken.None);

        Assert.Equal("SND-LINE-001", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_parents_are_read_from_an_index_json_in_order()
    {
        var index = LineFixture.Plan()["index.json"];

        Assert.Equal([Design, Scope], LinePacketIndex.Parents(index));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"kind\":\"design\"}")]
    [InlineData("{\"parents\":\"sha256:x\"}")]
    public void An_index_json_without_a_parents_list_has_none(string index) => Assert.Empty(LinePacketIndex.Parents(index));
}
