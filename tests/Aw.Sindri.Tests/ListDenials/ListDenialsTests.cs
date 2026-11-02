using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ListDenials;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Sandbox.Denials;

namespace Aw.Sindri.Tests.ListDenials;

/// <summary>'GET /sandbox/denials' (sandbox spec §3.6): the run areas' counters, optionally only what was last seen at or after a time.</summary>
[Lane(TestLane.Unit)]
public sealed class ListDenialsTests
{
    private static readonly DateTimeOffset _noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DenialEntry Seen(string target, DateTimeOffset last) => new(DenialKind.Network, target, DenialRules.NoGroup, 1, last, last);

    [Fact]
    public async Task Denials_since_filters_by_last_seen()
    {
        var old = Seen("old.example", _noon.AddHours(-3));
        var fresh = Seen("fresh.example", _noon.AddMinutes(-5));
        var stale = new DenialFile(LaunchId.From(DenialsRendererTests.LaunchTwo), _noon.AddHours(-3), [Seen("older.example", _noon.AddHours(-4))], [new DenialOverflow(DenialKind.Network, 9)]);
        var mixed = new DenialFile(LaunchId.From(DenialsRendererTests.LaunchOne), _noon, [old, fresh], [new DenialOverflow(DenialKind.Path, 2)]);
        var service = new ListDenialsService(new StubFiles(mixed, stale), new StubOwners());

        var result = await service.Handle(new ListDenialsRequest(Launch: null, Since: _noon.AddHours(-1)), Token);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value.Launches);
        Assert.Equal([fresh with { Fix = DenialFix.Allow }], only.Entries);
        Assert.Equal([new DenialOverflow(DenialKind.Path, 2)], only.Overflow);   // the file was updated since, so its overflow stays
    }

    [Fact]
    public async Task Without_since_every_file_comes_back_whole_and_the_launch_filter_goes_to_the_port()
    {
        var launch = LaunchId.From(DenialsRendererTests.LaunchOne);
        var files = new StubFiles(new DenialFile(launch, _noon, [Seen("a.example", _noon.AddDays(-9))], []));

        var result = await new ListDenialsService(files, new StubOwners()).Handle(new ListDenialsRequest(launch), Token);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(Assert.Single(result.Value.Launches).Entries);
        Assert.Equal(launch, files.Asked);
    }

    [Fact]
    public async Task The_item_filter_keeps_only_that_items_launches_and_names_their_repo()
    {
        var web = new DenialFile(LaunchId.From(DenialsRendererTests.LaunchOne), _noon, [Seen("a.example", _noon)], []);
        var api = new DenialFile(LaunchId.From(DenialsRendererTests.LaunchTwo), _noon, [Seen("b.example", _noon)], []);
        var owners = new StubOwners(
            (web.LaunchId, new LaunchOwner(A.FirstItemId, RepoName.From("web"))),
            (api.LaunchId, new LaunchOwner(ItemId.From("ITM-00000000000000000000000000000002"), RepoName.From("api"))));

        var result = await new ListDenialsService(new StubFiles(web, api), owners).Handle(new ListDenialsRequest(Item: A.FirstItemId), Token);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value.Launches);
        Expect.Equal((web.LaunchId, "web", A.FirstItemId), (only.LaunchId, only.Repo?.Value, only.Item));
    }

    [Fact]
    public async Task An_item_with_no_launches_has_no_denials()
    {
        var web = new DenialFile(LaunchId.From(DenialsRendererTests.LaunchOne), _noon, [Seen("a.example", _noon)], []);
        var owners = new StubOwners((web.LaunchId, new LaunchOwner(A.FirstItemId, RepoName.From("web"))));

        var result = await new ListDenialsService(new StubFiles(web), owners).Handle(new ListDenialsRequest(Item: ItemId.From("ITM-00000000000000000000000000000009")), Token);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Launches);
    }

    [Fact]
    public async Task Every_entry_carries_its_fix()
    {
        var launch = new DenialFile(
            LaunchId.From(DenialsRendererTests.LaunchOne), _noon,
            [
                Seen("github.com", _noon),
                new DenialEntry(DenialKind.Path, "/home/o/.cache/tool", DenialRules.NotBound, 1, _noon, _noon),
                new DenialEntry(DenialKind.Path, "/var/run/docker.sock", DenialRules.DenyList, 1, _noon, _noon),
                new DenialEntry(DenialKind.Policy, "git push", DenialRules.InnerPolicy, 1, _noon, _noon),
            ], []);

        var result = await new ListDenialsService(new StubFiles(launch), new StubOwners()).Handle(new ListDenialsRequest(), Token);

        var entries = Assert.Single(result.Value.Launches).Entries;
        Assert.Equal([DenialFix.Allow, DenialFix.Allow, DenialFix.Never, DenialFix.OwnTerminal], entries.Select(e => e.Fix!.Value));
        Assert.NotNull(entries[2].Reason);
        Assert.Null(entries[1].Reason);
    }

    private sealed class StubOwners(params (LaunchId Launch, LaunchOwner Owner)[] owners) : ILaunchOwners
    {
        public ValueTask<IReadOnlyDictionary<LaunchId, LaunchOwner>> Read(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<LaunchId, LaunchOwner>>(owners.ToDictionary(o => o.Launch, o => o.Owner));
    }

    private sealed class StubFiles(params DenialFile[] files) : IDenialFiles
    {
        public LaunchId? Asked { get; private set; }

        public ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken)
        {
            Asked = launch;
            return ValueTask.FromResult<IReadOnlyList<DenialFile>>(files);
        }
    }
}
