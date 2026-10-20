using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.freeze' on the block runtime (BS-1b, scoping spec §3 'Freeze'): re-check, confirm the hash, freeze through aw-packets, record {hash, dir}.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeFreezeStepTests
{
    private readonly InMemoryRunArea _area = new();
    private readonly InMemoryPackets _packets = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Dir => _area.PathFor(ScopingStepContexts.Address, "scope-packet").Value;

    private PacketRef Packet => new(PacketKind.Scoping, Dir);

    private ScopingFacts Facts(string? hash = null)
    {
        var facts = ScopingFacts.Expiry();
        facts["scopePacket"] = $$"""{"hash":"{{hash ?? InMemoryPackets.HashOf(Packet)}}"}""";
        return facts;
    }

    private Task<BlockStepResult> Run(ScopingFacts facts) =>
        new ScopeFreezeStep(_area, _packets).Run(ScopingStepContexts.Of("step.scope.freeze", facts), Token).AsTask();

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task A_packet_that_re_checks_clean_and_hashes_as_recorded_is_frozen_by_the_owner_and_records_its_hash_and_directory()
    {
        var result = await Run(Facts());

        Assert.Equal("frozen", result.Result!.Outcome.Value);
        var packet = Out(result, "scopePacket");
        Assert.Equal((InMemoryPackets.HashOf(Packet), Dir), (packet["hash"]!.GetValue<string>(), packet["dir"]!.GetValue<string>()));
        var freeze = Assert.Single(_packets.Calls, static c => c.Verb == "freeze");
        Assert.Equal((PacketKind.Scoping, Dir, PacketFreezer.Owner), (freeze.Packet.Kind, freeze.Packet.Directory, freeze.By));
    }

    [Fact]
    public async Task A_run_area_packet_that_hashes_to_something_else_than_fact_scopePacket_is_a_mismatch_with_findings_and_nothing_is_frozen()
    {
        var result = await Run(Facts("sha256:" + new string('a', 64)));

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        Assert.NotEmpty(Out(result, "findings").AsArray());
        Assert.DoesNotContain(_packets.Calls, static c => c.Verb == "freeze");
    }

    [Fact]
    public async Task A_packet_whose_facts_no_longer_pass_the_evidence_checks_is_a_mismatch_and_nothing_is_hashed_or_frozen()
    {
        var result = await Run(Facts().Without("challenge"));

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        Assert.NotEmpty(Out(result, "findings").AsArray());
        Assert.Empty(_packets.Calls);
    }

    [Fact]
    public async Task A_packet_aw_packets_refuses_is_a_mismatch_carrying_its_failed_checks()
    {
        _ = _packets.Fails(Dir, new PacketFailure("PKT-SCP-001", "scope-map.json", "bad", "fix it"));

        var result = await Run(Facts());

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        Assert.Contains("PKT-SCP-001", Out(result, "findings").ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_that_cannot_run_fails_the_Step()
    {
        _ = _packets.Unreadable(Dir);

        var result = await Run(Facts());

        Assert.Null(result.Result);
        Assert.Contains("aw-packets", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Step_without_fact_scopePacket_or_an_address_fails()
    {
        var missing = await Run(Facts().Without("scopePacket"));
        var step = new ScopeFreezeStep(_area, _packets);
        var noAddress = await step.Run(ScopingStepContexts.Of("step.scope.freeze", Facts()) with { Address = null }, Token);

        Assert.Null(missing.Result);
        Assert.Null(noAddress.Result);
    }

    [Fact]
    public async Task A_freeze_that_records_another_hash_than_the_one_the_owner_saw_is_a_mismatch_not_a_frozen()
    {
        var other = "sha256:" + new string('c', 64);
        var step = new ScopeFreezeStep(_area, new FreezesAs(_packets, other));

        var result = await step.Run(ScopingStepContexts.Of("step.scope.freeze", Facts()), Token);

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        Assert.Contains(other, Out(result, "findings").ToJsonString(), StringComparison.Ordinal);
    }

    /// <summary>Hashes as the inner fake does and freezes under another hash, as a packet edited between the two calls would.</summary>
    private sealed class FreezesAs(InMemoryPackets inner, string hash) : IPackets
    {
        public ValueTask<Result<PacketValidation>> Validate(PacketRef packet, CancellationToken cancellationToken) => inner.Validate(packet, cancellationToken);

        public ValueTask<Result<string>> Hash(PacketRef packet, CancellationToken cancellationToken) => inner.Hash(packet, cancellationToken);

        public ValueTask<Result<PacketFreeze>> Freeze(PacketRef packet, PacketFreezer by, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<PacketFreeze>>(new PacketFreeze(Frozen: true, hash, by, AlreadyFrozen: false, AlreadyFrozenBy: null, Failures: []));
    }
}
