using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The opt-in component hop through the node-shape query (C4): the related feature, the skipped list and the 140 refusal.</summary>
public sealed class ComponentHopQueryTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task With_the_hop_a_feature_sharing_a_code_unit_is_an_affects_item_and_without_it_nothing_is()
    {
        await Import(2);

        var on = (await Query("component")).Value;
        var off = (await Query(hops: null)).Value;

        var reach = Assert.Single(Section(on, "affects").Items);
        Assert.Equal(("feature:f1", 1), (reach.Node, reach.Depth));
        Assert.Equal(["feature:f0", "component code:src/shared.ts", "implements feature:f1"], reach.Path, StringComparer.Ordinal);
        Assert.Null(on.SkippedComponents);
        Assert.Empty(off.Sections.Where(s => s.Name == "affects").SelectMany(s => s.Items));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_unit_over_the_cap_is_listed_in_skipped_components_with_its_feature_count()
    {
        await Import(51);

        var result = (await Query("component")).Value;

        Assert.Equal([new SkippedComponent("code:src/shared.ts", 51)], result.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_hop_name_is_refused_with_140()
    {
        await Import(2);

        var result = await Query("neighbors");

        Assert.Equal("SND-CONTEXT-140", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lenses_and_sets_take_the_hop_list_and_refuse_an_unknown_name_with_140()
    {
        await Import(51);

        var one = await _world.Engine.Lenses("feature:f0", MapRead.Current, new LensRange(1, "component"), MapWorld.Human, MapWorld.None);
        var set = await _world.Engine.LensesOf(["feature:f0", "feature:f1"], MapRead.Current, new LensRange(1, "component"), MapWorld.Human, MapWorld.None);
        var bad = await _world.Engine.Lenses("feature:f0", MapRead.Current, new LensRange(1, "everything"), MapWorld.Human, MapWorld.None);

        Assert.Equal([new SkippedComponent("code:src/shared.ts", 51)], one.Value.SkippedComponents);
        Assert.Equal(["component"], one.Value.Searched.Hops);
        Assert.Equal([new SkippedComponent("code:src/shared.ts", 51)], set.Value.SkippedComponents);
        Assert.Equal("SND-CONTEXT-140", bad.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_hops_echo_is_in_cascade_order_whatever_order_the_request_listed_them()
    {
        await Import(2);

        var one = await _world.Engine.Lenses("feature:f0", MapRead.Current, new LensRange(1, "component,flow,state,dependency"), MapWorld.Human, MapWorld.None);

        Assert.Equal(["state", "dependency", "flow", "component"], one.Value.Searched.Hops);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_skipped_unit_key_holding_a_secret_is_scrubbed_in_the_lenses_answer_and_the_query_answer()
    {
        var unit = "code:src/" + FakeScrubber.Secret + ".ts";
        await Import(51, unit);

        var lenses = await _world.Engine.Lenses("feature:f0", MapRead.Current, new LensRange(1, "component"), MapWorld.Human, MapWorld.None);
        var set = await _world.Engine.LensesOf(["feature:f0", "feature:f1"], MapRead.Current, new LensRange(1, "component"), MapWorld.Human, MapWorld.None);
        var query = await Query("component");

        foreach (var skipped in new[] { lenses.Value.SkippedComponents, set.Value.SkippedComponents, query.Value.SkippedComponents })
        {
            var only = Assert.Single(skipped!);
            Assert.DoesNotContain(FakeScrubber.Secret, only.Unit, StringComparison.Ordinal);
            Assert.Contains("[REDACTED:fake]", only.Unit, StringComparison.Ordinal);
            Assert.Equal(51, only.Features);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_fan_out_of_10_reaches_the_lenses_and_query_services_so_a_unit_of_6_features_is_walked()
    {
        await Import(6);
        var scope = MapWorld.Human with { Settings = System.Text.Json.JsonDocument.Parse("""{"cascadeComponentFanOut":10}""").RootElement.Clone() };

        var lenses = await _world.Engine.Lenses("feature:f0", MapRead.Current, new LensRange(1, "component"), scope, MapWorld.None);
        var query = await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:f0") { Depth = 1, Hops = "component" }, scope, MapWorld.None);

        Assert.Null(lenses.Value.SkippedComponents);
        Assert.Equal(5, lenses.Value.Bands.Product.Affects.Count);
        Assert.Null(query.Value.SkippedComponents);
        Assert.Equal(5, Section(query.Value, "affects").Items.Count);
    }

    private async Task<Result<ContextResult>> Query(string? hops) =>
        await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:f0") { Depth = 1, Hops = hops }, MapWorld.Human, MapWorld.None);

    private async Task Import(int features, string unit = "code:src/shared.ts")
    {
        var keys = Enumerable.Range(0, features).Select(i => string.Create(CultureInfo.InvariantCulture, $"feature:f{i}")).ToList();
        var lines = new List<string> { "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:" };
        lines.AddRange(keys.Select(k => $"  - {{key: \"{k}\", label: \"{k[8..]}\"}}"));
        lines.Add($"  - {{key: \"{unit}\", label: \"shared\"}}");
        lines.Add("edges:");
        lines.AddRange(keys.Select(k => $"  - {{from: \"{unit}\", to: \"{k}\", type: implements}}"));
        _ = await _world.ImportYaml(MapWorld.Yaml([.. lines]));
    }

    private static ResultSection Section(ContextResult result, string name) => result.Sections.Single(s => s.Name == name);
}
