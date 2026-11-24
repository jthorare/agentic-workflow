using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Graph;

/// <summary>
/// The "Did you mean" choices of SND-CONTEXT-003 and -037 on the paths #3045 left (#4196): history, lenses with several anchors, the code lens and the
/// map status with an anchor. Each scrubs a label through the same MapText.ChoiceLabel as a question's suggestions, so a planted secret never leaves.
/// </summary>
public sealed class AnchorChoiceScrubRestTests : IDisposable
{
    private const string Scrubbed = "Tab groups [REDACTED:fake]";
    private const string Unknown = "tab groups zzz";
    private const string Shared = "Tab groups " + FakeScrubber.Secret;
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Mcp => MapWorld.HumanVia(ContextClient.Mcp);

    private Task<MapImported> Import() => _world.ImportYaml(MapWorld.Yaml(
        "schemaVersion: 1",
        "kind: authored",
        "repo: aw-split-panes",
        "nodes:",
        "  - {key: \"feature:restore\", label: \"Restore\"}",
        "  - {key: \"concept:tabs.tab-groups\", label: \"" + Shared + "\"}",
        "  - {key: \"concept:tabs.tab-groups-2\", label: \"" + Shared + "\"}"));

    private static void AssertScrubbed(Kernel.Error error, string code)
    {
        Assert.Equal(code, error.Code);
        var member = Assert.Single(error.Extensions ?? Kernel.ErrorExtensions.Empty, m => m.Key == "choices");
        var labels = member.Value.EnumerateArray().Select(e => e.GetProperty("label").GetString() ?? string.Empty).ToList();
        Assert.Contains(Scrubbed, labels);
        Assert.All(labels, l => Assert.DoesNotContain(FakeScrubber.Secret, l, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task History_of_an_unknown_name_lists_scrubbed_choices()
    {
        _ = await Import();

        var history = await _world.Engine.History([Unknown], HistoryWindow.Of(since: null, before: null).Value, MapRead.Current, Mcp, MapWorld.None);

        AssertScrubbed(history.Error, ContextErrors.UnknownAnchor.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task History_of_a_shared_name_lists_scrubbed_choices()
    {
        _ = await Import();

        var history = await _world.Engine.History([Shared], HistoryWindow.Of(since: null, before: null).Value, MapRead.Current, Mcp, MapWorld.None);

        AssertScrubbed(history.Error, ContextErrors.AmbiguousAnchor.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lenses_of_several_unknown_names_list_scrubbed_choices()
    {
        _ = await Import();

        var lenses = await _world.Engine.LensesOf([Unknown, "zzz yyy"], MapRead.Current, depth: 0, Mcp, MapWorld.None);

        AssertScrubbed(lenses.Error, ContextErrors.UnknownAnchor.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lenses_of_several_anchors_with_a_shared_name_list_scrubbed_choices()
    {
        _ = await Import();

        var lenses = await _world.Engine.LensesOf([Shared, "Restore"], MapRead.Current, depth: 0, Mcp, MapWorld.None);

        AssertScrubbed(lenses.Error, ContextErrors.AmbiguousAnchor.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_scope_graph_of_an_unknown_name_lists_scrubbed_choices()
    {
        _ = await Import();

        var graph = await _world.Engine.ScopeGraph(new ScopeGraphQuery([Unknown], MapRead.Current, new LensRange(0, Hops: null), Brief: null, MaxNodes: 400, MaxBytes: 300_000), Mcp, MapWorld.None);

        AssertScrubbed(graph.Error, ContextErrors.UnknownAnchor.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_code_lens_of_an_unknown_name_lists_scrubbed_choices()
    {
        _ = await Import();

        var code = await _world.Engine.Code([Unknown], draft: false, mapVersion: null, Mcp, MapWorld.None);

        AssertScrubbed(code.Error, ContextErrors.UnknownAnchor.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_map_status_of_an_unknown_name_lists_scrubbed_choices()
    {
        _ = await Import();

        var status = await _world.Engine.MapStatus(Unknown, Mcp, MapWorld.None);

        AssertScrubbed(status.Error, ContextErrors.UnknownAnchor.Code);
    }
}
