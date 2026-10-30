using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Surfaces;
using Aw.Context.Testing.Code;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Surfaces;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.BuildMap;

/// <summary>The product-surface step of a build (model routing spec 6.9): Jev's reject and merge rows ride in the draft, a stored verdict is reused, and nothing is asked when the pick is off.</summary>
[Trait("Lane", "unit")]
public sealed class BuildMapSurfaceTests : IDisposable
{
    private const string Split = "action:command/split-vertical";
    private const string Close = "action:command/close-pane";
    private const string PickOn = """{"repos":["aw-split-panes"],"models":{"productSurfaces":{"pick":"jev"}}}""";
    private const string PickOff = """{"repos":["aw-split-panes"]}""";
    private readonly FakeSurfacePicker _picker = new();
    private readonly FakeSurfacePickStore _store = new();
    private readonly MapWorld _world;

    public BuildMapSurfaceTests()
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal)
        {
            ["src/commands/registry.ts"] = "register({ id: \"split-vertical\", title: \"Split vertically\" });\nregister({ id: \"close-pane\", title: \"Close pane\" });",
        };
        _world = new MapWorld(
            new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files),
            codeSearch: new FakeCodeSearch(MapWorld.Repos[0], MapWorld.Head, files),
            configure: services => services.AddSingleton<ISurfacePicker>(_picker).AddSingleton<ISurfacePickStore>(_store));
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task A_reject_verdict_puts_a_jev_reject_row_for_each_candidate_in_the_draft()
    {
        _picker.When(Split, g => SurfaceVerdict.Rejected(g.Key));

        _ = await Build(PickOn);

        var rows = await DraftRows();
        Assert.Equal([Close, Split], rows.Where(r => r.Action == OverrideAction.Reject).Select(r => r.Target.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(rows, r => Assert.Equal(MapOverride.JevWho, r.Who));
    }

    [Fact]
    public async Task A_rejected_action_is_gone_from_the_draft_as_read_and_back_after_an_undo_of_its_row()
    {
        _picker.When(Split, g => SurfaceVerdict.Rejected(g.Key));
        _ = await Build(PickOn);
        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var jev = draft.Overrides.Where(o => o.Who == MapOverride.JevWho).ToList();

        var served = OverrideApplier.Apply(draft.Graph, draft.Overrides).Graph;
        Assert.Null(served.Resolve(NodeKey.From(Split)));
        Assert.NotNull(draft.Graph.Resolve(NodeKey.From(Split)));   // the node stays in the stored draft, so review can show it

        var undo = jev.Select(r => new MapOverride(OverrideAction.Undo, r.Target, "joi", r.At.AddHours(1), Undoes: r.ToId())).ToList();
        Assert.NotNull(OverrideApplier.Apply(draft.Graph, [.. draft.Overrides, .. undo]).Graph.Resolve(NodeKey.From(Split)));
    }

    [Fact]
    public async Task A_merge_verdict_puts_a_jev_merge_row_into_the_survivor()
    {
        _picker.When(Split, g => SurfaceVerdict.Merged(g.Key, g.Candidates.ToList().FindIndex(c => c.NodeKey == Split), Split, "Split vertically"));

        _ = await Build(PickOn);

        var merge = Assert.Single(await DraftRows());
        Assert.Equal((OverrideAction.Merge, Close, Split), (merge.Action, merge.Target.Value, merge.Other?.Value));
    }

    [Fact]
    public async Task A_rebuild_with_nothing_new_asks_nothing_because_the_decided_verdict_is_stored()
    {
        _picker.When(Split, g => SurfaceVerdict.Rejected(g.Key));
        _ = await Build(PickOn);
        Assert.Equal(1, _picker.Calls);

        _ = await Build(PickOn);

        Assert.Equal(1, _picker.Calls);
        Assert.Equal(2, (await DraftRows()).Count);   // the stored verdict still writes its rows into the new draft
    }

    [Fact]
    public async Task With_the_pick_off_no_rows_are_written_and_the_build_says_not_decided()
    {
        _picker.NotDecided = "off";

        var report = await Build(PickOff);

        Assert.Empty(await DraftRows());
        Assert.Contains("product surfaces: not decided (off)", report.Notes, StringComparer.Ordinal);
        Assert.False(_picker.Options.Single().Pick);
    }

    [Fact]
    public async Task An_action_a_person_already_accepted_is_never_sent_to_jev()
    {
        _ = await _world.ImportFixture();
        _ = await _world.Store.AppendOverrides([new MapOverride(OverrideAction.Accept, NodeKey.From(Split), "joi", DateTimeOffset.UnixEpoch)], MapWorld.None);

        _ = await Build(PickOn);

        var asked = _picker.Asked.SelectMany(g => g).SelectMany(g => g.Candidates).Select(c => c.NodeKey);
        Assert.DoesNotContain(Split, asked, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_picker_is_told_what_labeling_spent_this_build()
    {
        _world.Clusters.Set = new ClusterSet([new FileCluster(ClusterId.From("c1"), ["src/commands/ids.ts", "src/commands/registry.ts"], 0.7)], Available: true);
        _world.Labeler.Spent = 321;
        _ = await Build("""{"repos":["aw-split-panes"],"labeling":{"enabled":true,"maxTokens":1500},"models":{"productSurfaces":{"pick":"jev"}}}""");

        var options = _picker.Options.Single();
        Assert.True(options.Pick);
        Assert.Equal((321, 1500), (options.SpentTokens, options.MaxTokens));
    }

    private async Task<MapBuildReport> Build(string section) => (await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(section), MapWorld.None)).Value;

    private async Task<IReadOnlyList<MapOverride>> DraftRows() => [.. (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Overrides.Where(o => o.Who == MapOverride.JevWho)];
}
