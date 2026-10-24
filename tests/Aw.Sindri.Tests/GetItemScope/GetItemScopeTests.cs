using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetItemScope;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.GetItemScope;

/// <summary>An item's scope (GET /sindri/items/{id}/scope): the anchors and map versions in the lenses.json of its latest approved scoping packet.</summary>
[Lane(TestLane.Unit)]
public sealed class GetItemScopeTests
{
    private const string Address = "ENG-1/scoping/1";
    private const string OtherAddress = "ENG-1/scoping/2";
    private static readonly string _hash = "sha256:" + new string('a', 64);
    private static readonly string _otherHash = "sha256:" + new string('b', 64);

    private readonly ItemQueryHarness _q = new();
    private readonly PacketsByInstance _store = new();
    private readonly CapturingLogger _log = new();
    private LeaseGrant? _grant;

    private GetItemScopeService Service() => new(new ViewItemResolver(_q.Views.Views), _q.Views.Slice.Callers, _q.Views.Slice.Store, _q.Views.Slice.Events, _store, _log);

    private static WorkflowInstanceId Instance(string address) => WorkflowInstanceId.Of(InstanceAddress.From(address));

    private async Task Approve(char item, string address, string hash)
    {
        _grant ??= await _q.Views.Slice.HoldLease();
        _q.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        var payload = new ScopingApproved(hash, address, ["S1"], ["AC-1"], Rounds: 1, Rescopes: 0);
        var key = "scoping.approved:" + hash;
        _ = (await _q.Views.Slice.Store.Append(_q.Views.Slice.Events.Entry(EventTypes.ScopingApproved, payload, key, ItemQueryHarness.Id(item).Value), _grant, ItemQueryHarness.Token)).Value;
    }

    private static string Lenses(string code, string product, string related, string history) =>
        $$"""{"code":{{Lens(code)}},"product":{{Lens(product)}},"related":{{Lens(related)}},"history":{{Lens(history)}}}""";

    /// <summary>A lens entry: "v3@abc1234:key,key" is the map version, then the anchors.</summary>
    private static string Lens(string spec)
    {
        var parts = spec.Split(':', 2);
        var anchors = string.Join(',', parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(static a => $"\"{a}\""));
        return $$"""{"anchors":[{{anchors}}],"depth":2,"mapVersion":"{{parts[0]}}","foundCount":0}""";
    }

    private async Task Frozen(string address, string hash, string lenses)
    {
        _store.Put(Instance(address), hash, lenses);
        await Approve('1', address, hash);
    }

    private async Task<Result<ItemScopeResponse>> Ask(char item = '1') =>
        await Service().Handle(A.GetItemScopeRequest().WithItem(ItemRef.To(ItemQueryHarness.Id(item))).Build(), ItemQueryHarness.Token);

    [Fact]
    public async Task An_item_with_a_frozen_scoping_packet_answers_its_anchors_and_map_version()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses(
            "v7@abc1234:feature:tabs.tab-manager,code:src/tabs.ts", "v7@abc1234:feature:tabs.tab-manager", "v7@abc1234:", "v7@abc1234:code:src/a.ts"));
        using var entered = _q.Human();

        var answer = await Ask();

        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.ToString() : null);
        var scope = answer.Value.Scope!;
        Expect.Equal(ItemQueryHarness.Id('1'), answer.Value.Item);
        Expect.Sequence(["code:src/a.ts", "code:src/tabs.ts", "feature:tabs.tab-manager"], [.. scope.Anchors.Select(a => a.Key)]);
        Assert.All(scope.Anchors, a => Expect.Equal("v7", a.MapVersion));
        Expect.Equal("v7", scope.MapVersion);
        Expect.Equal("abc1234", scope.MapCommit);
        Expect.Equal(new ScopeMapVersions("v7", "v7", "v7", "v7"), scope.MapVersions);
        Expect.Equal(_hash, scope.PacketHash);
        Expect.Equal(_q.Views.Slice.Clock.GetUtcNow(), scope.FrozenAt);
    }

    [Fact]
    public async Task Lenses_read_at_different_versions_have_no_common_version_and_keep_each_anchors_own()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:feature:a", "v8@def5678:feature:a,code:b", "v7@abc1234:"));
        using var entered = _q.Human();

        var scope = (await Ask()).Value.Scope!;

        Assert.Null(scope.MapVersion);
        Assert.Null(scope.MapCommit);
        Expect.Equal(new ScopeMapVersions("v7", "v7", "v8", "v7"), scope.MapVersions);
        Expect.Sequence([("code:b", "v8"), ("feature:a", "v7"), ("feature:a", "v8")], [.. scope.Anchors.Select(a => (a.Key, a.MapVersion))]);
    }

    [Fact]
    public async Task Lenses_at_one_version_of_different_commits_name_the_version_and_no_commit()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@def5678:", "v7@abc1234:"));
        using var entered = _q.Human();

        var scope = (await Ask()).Value.Scope!;

        Expect.Equal("v7", scope.MapVersion);
        Assert.Null(scope.MapCommit);
    }

    [Fact]
    public async Task An_item_with_no_approved_scoping_packet_has_no_scope()
    {
        await _q.Item('1', "Ship it", "demo");
        _store.Put(Instance(Address), _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));   // a draft in a run area, never approved
        using var entered = _q.Human();

        var answer = await Ask();

        Assert.True(answer.IsSuccess);
        Assert.Null(answer.Value.Scope);
    }

    [Fact]
    public async Task The_latest_approved_packet_wins()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses("v7@abc1234:feature:old", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));
        await Frozen(OtherAddress, _otherHash, Lenses("v9@abc1234:feature:new", "v9@abc1234:", "v9@abc1234:", "v9@abc1234:"));
        using var entered = _q.Human();

        var scope = (await Ask()).Value.Scope!;

        Expect.Equal(_otherHash, scope.PacketHash);
        Expect.Sequence(["feature:new"], [.. scope.Anchors.Select(a => a.Key)]);
    }

    [Fact]
    public async Task Another_items_approved_packet_is_not_this_items_scope()
    {
        await _q.Item('1', "Ship it", "demo");
        await _q.Item('2', "Other", "demo");
        _store.Put(Instance(Address), _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));
        await Approve('2', Address, _hash);
        using var entered = _q.Human();

        Assert.Null((await Ask('1')).Value.Scope);
        Assert.NotNull((await Ask('2')).Value.Scope);
    }

    [Fact]
    public async Task A_packet_that_no_longer_hashes_to_its_approval_has_no_scope()
    {
        await _q.Item('1', "Ship it", "demo");
        _store.Put(Instance(Address), _otherHash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));
        await Approve('1', Address, _hash);
        using var entered = _q.Human();

        var answer = await Ask();

        Assert.True(answer.IsSuccess);
        Assert.Null(answer.Value.Scope);
        ExpectOneWarning("its packet no longer hashes to the approved hash");
    }

    [Fact]
    public async Task An_approval_whose_run_area_is_gone_has_no_scope()
    {
        await _q.Item('1', "Ship it", "demo");
        await Approve('1', Address, _hash);
        using var entered = _q.Human();

        var answer = await Ask();

        Assert.True(answer.IsSuccess);
        Assert.Null(answer.Value.Scope);
        ExpectOneWarning("its packet is not in the run area");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{"code":{"anchors":[],"mapVersion":"v7@abc1234"}}""")]   // a lens is missing
    [InlineData("""{"code":{"anchors":[],"mapVersion":"7@abc1234"},"product":{"anchors":[],"mapVersion":"v7@abc1234"},"related":{"anchors":[],"mapVersion":"v7@abc1234"},"history":{"anchors":[],"mapVersion":"v7@abc1234"}}""")]
    [InlineData("""{"code":{"anchors":[],"mapVersion":"v7"},"product":{"anchors":[],"mapVersion":"v7@abc1234"},"related":{"anchors":[],"mapVersion":"v7@abc1234"},"history":{"anchors":[],"mapVersion":"v7@abc1234"}}""")]
    [InlineData("""{"code":{"anchors":[],"mapVersion":"v7@"},"product":{"anchors":[],"mapVersion":"v7@abc1234"},"related":{"anchors":[],"mapVersion":"v7@abc1234"},"history":{"anchors":[],"mapVersion":"v7@abc1234"}}""")]
    [InlineData("""{"code":{"anchors":[],"mapVersion":"v7@abc1234"},"product":{"anchors":[],"mapVersion":null},"related":{"anchors":[],"mapVersion":"v7@abc1234"},"history":{"anchors":[],"mapVersion":"v7@abc1234"}}""")]
    public async Task A_lenses_file_that_does_not_read_has_no_scope_and_one_warning(string lenses)
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, lenses);
        using var entered = _q.Human();

        var answer = await Ask();

        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.ToString() : null);
        Assert.Null(answer.Value.Scope);
        ExpectOneWarning("its lenses.json does not read");
    }

    [Fact]
    public async Task A_lenses_file_that_cannot_be_read_has_no_scope_and_one_warning()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));
        _store.FailReads = true;
        using var entered = _q.Human();

        var answer = await Ask();

        Assert.True(answer.IsSuccess);
        Assert.Null(answer.Value.Scope);
        ExpectOneWarning("its lenses.json cannot be read");
    }

    [Fact]
    public async Task A_hash_that_cannot_be_computed_is_named_a_hash_failure()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));
        _store.FailHash = true;
        using var entered = _q.Human();

        var answer = await Ask();

        Assert.True(answer.IsSuccess);
        Assert.Null(answer.Value.Scope);
        ExpectOneWarning("its packet could not be hashed");
    }

    [Fact]
    public async Task A_scope_that_reads_logs_nothing()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));
        using var entered = _q.Human();

        _ = await Ask();

        Expect.Equal(0, _log.Lines.Count);
    }

    [Fact]
    public async Task An_unknown_item_is_not_found()
    {
        using var entered = _q.Human();

        var answer = await Ask('9');

        Assert.True(answer.IsError);
        Expect.Equal("SND-ITEM-404", answer.Error.Code);
    }

    [Fact]
    public async Task A_session_of_another_repo_gets_the_scope_too()
    {
        await _q.Item('1', "Ship it", "demo");
        await Frozen(Address, _hash, Lenses("v7@abc1234:feature:a", "v7@abc1234:", "v7@abc1234:", "v7@abc1234:"));
        using var entered = _q.Session("other");

        var answer = await Ask();

        Assert.True(answer.IsSuccess);
    }

    /// <summary>Exactly one warning, with only the item id, the ledger row and the reason: no hash, path or lens text.</summary>
    private void ExpectOneWarning(string reason)
    {
        var line = Expect.Only(_log.Lines);
        var item = ItemQueryHarness.Id('1').Value;
        Assert.Matches($@"^Warning item-scope: {item} has an approved scoping packet \(ledger row [0-9]+\) but {System.Text.RegularExpressions.Regex.Escape(reason)}; answering no scope$", line);
    }

    private sealed class CapturingLogger : ILogger<GetItemScopeService>
    {
        internal List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add($"{logLevel} {formatter(state, exception)}");
    }

    /// <summary>Run areas by instance: the packet's hash and its lenses.json.</summary>
    private sealed class PacketsByInstance : IScopePacketStore
    {
        private readonly Dictionary<WorkflowInstanceId, (string Hash, string Lenses)> _packets = [];

        public bool FailReads { get; set; }

        public bool FailHash { get; set; }

        public void Put(WorkflowInstanceId instance, string hash, string lenses) => _packets[instance] = (hash, lenses);

        public ValueTask<Result<string>> Hash(WorkflowInstanceId instance, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<string>>(FailHash ? ScopingErrors.PacketToolFailed.With("aw-packets timed out") : _packets.TryGetValue(instance, out var packet) ? packet.Hash : SindriErrors.ItemNotFound.With("no packet"));

        public ValueTask<Result<string>> ReadFile(WorkflowInstanceId instance, string name, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<string>>(!FailReads && _packets.TryGetValue(instance, out var packet) && name == "lenses.json" ? packet.Lenses : SindriErrors.ItemNotFound.With("no file"));
    }
}
