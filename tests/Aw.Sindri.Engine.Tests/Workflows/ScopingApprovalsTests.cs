using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The engine's 'scoping.approved' row (BS-1b, api #503): appended under the instance's lease when 'step.scope.freeze' reports 'frozen', once per packet hash.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopingApprovalsTests
{
    private const string Address = "T-1/scoping/1";
    private static readonly BlockId _freeze = BlockId.From("step.scope.freeze");

    private readonly InMemoryRunArea _area = new();
    private readonly InMemoryPackets _packets = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Dir => _area.PathFor(Address, "scope-packet").Value;

    // computed from the fake's own rule (kind|directory) over the literal directory, not through the code under test
    private static string Hash { get; } = "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Scoping|/run/T-1/scoping/1/scope-packet")));

    private async Task Packet(string rounds = "{\"round\":1,\"trigger\":\"first\"}\n{\"round\":2,\"trigger\":\"revise\"}\n")
    {
        _ = await _area.Write(Address, "scope-packet/scope-map.json", """{"schemaVersion":1,"surfaces":[{"id":"S1","title":"a"},{"id":"S2","title":"b"}]}""", append: false, Token);
        _ = await _area.Write(Address, "scope-packet/acceptance.json", """{"schemaVersion":1,"items":[{"id":"AC-1","text":"x"},{"id":"AC-2","text":"y"},{"id":"AC-3","text":"z"}]}""", append: false, Token);
        _ = await _area.Write(Address, "scope-packet/rounds.jsonl", rounds, append: false, Token);
    }

    private static StepContext Context(string address = Address) =>
        new(_freeze, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), InjectionSections.Empty) { Address = address };

    private BlockStepResult Frozen(string? hash = null)
    {
        using var packet = JsonDocument.Parse($$"""{"hash":"{{hash ?? Hash}}","dir":"{{Dir}}"}""");
        return BlockStepResult.Completed(OutcomeName.From("frozen"), new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:scopePacket")] = packet.RootElement.Clone() });
    }

    private static async Task<(ScopingApprovals Approvals, ILedgerStore Ledger, HeldLease Lease)> Open(InMemoryRunArea area, IPackets packets, bool held = true)
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var probe = new FakeProcessProbe();
        var store = new InMemorySindriStore(clock, probe);
        var lease = new HeldLease();
        if (held)
        {
            lease.Hold((await store.Acquire(probe.Self(), Token)).Value);
        }

        var ids = new SequentialIdGenerator();
        var events = new EventFactory(EventScope.ControlPlane, clock, ids, SliceHarness.Json, new AsyncLocalCallerContextAccessor(ids));
        return (new ScopingApprovals(store, lease, events, area, packets), store, lease);
    }

    private static async Task<IReadOnlyList<LedgerEvent>> Rows(ILedgerStore ledger) =>
        [.. (await ledger.ReadAfter(0, 100, Token)).Value.Where(static e => e.Envelope.Type == EventTypes.ScopingApproved)];

    private static ScopingApproved Payload(LedgerEvent row) => JsonSerializer.Deserialize<ScopingApproved>(row.Envelope.Payload, SliceHarness.Json)!;

    [Fact]
    public async Task Frozen_appends_one_scoping_approved_row_with_the_ids_and_counts_of_the_frozen_packet()
    {
        await Packet("{\"round\":1,\"trigger\":\"first\"}\n{\"round\":2,\"trigger\":\"rescope\"}\n{\"round\":3,\"trigger\":\"revise\"}\n");
        var (approvals, ledger, _) = await Open(_area, _packets);

        var result = await approvals.After(_freeze, Context(), Frozen(), Token);

        Assert.Null(result.Failure);
        var payload = Payload(Assert.Single(await Rows(ledger)));
        Assert.Equal(["S1", "S2"], payload.Surfaces);
        Assert.Equal(["AC-1", "AC-2", "AC-3"], payload.AcceptanceItems);
        Assert.Equal((3, 1), (payload.Rounds, payload.Rescopes));
    }

    [Fact]
    public async Task The_row_names_the_instance_by_its_address()
    {
        await Packet();
        var (approvals, ledger, _) = await Open(_area, _packets);

        _ = await approvals.After(_freeze, Context(), Frozen(), Token);

        Assert.Equal(Address, Payload(Assert.Single(await Rows(ledger))).Instance);
    }

    [Fact]
    public async Task The_row_is_about_the_item_so_api_can_scan_by_item_and_type()
    {
        await Packet();
        var (approvals, ledger, _) = await Open(_area, _packets);

        _ = await approvals.After(_freeze, Context(), Frozen(), Token);

        Assert.Equal("T-1", Assert.Single(await Rows(ledger)).Item);
    }

    [Fact]
    public async Task The_packet_hash_is_what_aw_packets_hashes_the_run_area_scope_packet_to_and_the_directory_stays_in_place()
    {
        await Packet();
        var (approvals, ledger, _) = await Open(_area, _packets);

        _ = await approvals.After(_freeze, Context(), Frozen(), Token);

        var row = Assert.Single(await Rows(ledger));
        Assert.Equal(Hash, Payload(row).PacketHash);
        Assert.Equal($"scoping.approved:{Hash}", row.IdempotencyKey);
        Assert.Contains("S1", (await _area.Read(Address, "scope-packet/scope-map.json", Token)).Value, StringComparison.Ordinal);   // nothing was moved or deleted
    }

    [Fact]
    public async Task A_Step_hash_that_is_not_the_kept_packets_hash_fails_the_Step_and_writes_no_row()
    {
        await Packet();
        var (approvals, ledger, _) = await Open(_area, _packets);

        var result = await approvals.After(_freeze, Context(), Frozen("sha256:" + new string('b', 64)), Token);

        Assert.Null(result.Result);
        Assert.Contains("hashes to", result.Failure, StringComparison.Ordinal);
        Assert.Empty(await Rows(ledger));
    }

    [Fact]
    public async Task An_address_that_names_no_item_fails_the_Step_instead_of_throwing()
    {
        var (approvals, ledger, _) = await Open(_area, _packets);

        var result = await approvals.After(_freeze, Context("nowhere"), Frozen(), Token);

        Assert.Null(result.Result);
        Assert.Empty(await Rows(ledger));
    }

    [Fact]
    public async Task The_same_freeze_reported_again_writes_nothing()
    {
        await Packet();
        var (approvals, ledger, _) = await Open(_area, _packets);

        _ = await approvals.After(_freeze, Context(), Frozen(), Token);
        _ = await approvals.After(_freeze, Context(), Frozen(), Token);
        _ = await _area.Write(Address, "scope-packet/rounds.jsonl", "{\"round\":1}\n", append: false, Token);
        _ = await approvals.After(_freeze, Context(), Frozen(), Token);

        _ = Assert.Single(await Rows(ledger));
    }

    [Fact]
    public async Task Other_outcomes_and_other_Steps_write_no_row()
    {
        await Packet();
        var (approvals, ledger, _) = await Open(_area, _packets);
        var mismatch = BlockStepResult.Completed(OutcomeName.From("mismatch"), new Dictionary<FactKey, JsonElement>());

        _ = await approvals.After(_freeze, Context(), mismatch, Token);
        _ = await approvals.After(BlockId.From("step.scope.check"), Context(), Frozen(), Token);
        _ = await approvals.After(_freeze, Context(), BlockStepResult.Failed("no"), Token);

        Assert.Empty(await Rows(ledger));
    }

    [Fact]
    public async Task Without_a_lease_the_Step_fails_instead_of_reporting_a_freeze_nothing_recorded()
    {
        await Packet();
        var (approvals, ledger, _) = await Open(_area, _packets, held: false);

        var result = await approvals.After(_freeze, Context(), Frozen(), Token);

        Assert.Null(result.Result);
        Assert.Contains("lease", result.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await Rows(ledger));
    }
}
