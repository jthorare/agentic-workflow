using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Search;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SearchMap;

/// <summary>The ranked map search through the engine: title over path over text, partial words, limit and total, the area, scope and fence.</summary>
public sealed class SearchMapTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string FirstDecision = "decision:d036352234b17baf9a7b2e7cffef599bdc9c8209e92c389d2bcbb919c74c162b";
    private readonly MapWorld _world = new();
    private bool _imported;

    public void Dispose() => _world.Dispose();

    private async Task<Kernel.Result<MapSearchView>> Ask(string q, int? limit = null, string? area = null, MapRead? read = null, ContextScope? scope = null)
    {
        if (!_imported)
        {
            _ = await _world.ImportYaml(MapWorld.TabsMap);
            _imported = true;
        }

        return await _world.Engine.Search(new MapSearchQuery(q, limit, area, Repo: null, read ?? MapRead.Current), scope ?? MapWorld.Human, MapWorld.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_title_match_ranks_first_and_says_it_matched_the_title()
    {
        var view = (await Ask("tab manager")).Value;

        Assert.Equal(Manager, view.Hits[0].Key.Value);
        Assert.Equal("title", view.Hits[0].Matched);
        Assert.Equal("v1", view.MapVersion);
        Assert.True(view.Hits.Zip(view.Hits.Skip(1), (a, b) => a.Score >= b.Score).All(static ok => ok), "hits are best first");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_last_word_matches_as_a_prefix()
    {
        var view = (await Ask("tab manag")).Value;

        Assert.Contains(view.Hits, h => h.Key.Value == Manager);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_word_only_the_decisions_quote_holds_finds_the_decision_by_its_text()
    {
        var view = (await Ask("single")).Value;

        var hit = Assert.Single(view.Hits);
        Assert.Equal(FirstDecision, hit.Key.Value);
        Assert.Equal("text", hit.Matched);
        Assert.Contains("single tab", hit.Snippet, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_path_finds_the_code_nodes_by_their_path()
    {
        var view = (await Ask("src/tabs")).Value;

        Assert.Equal(["code:src/tabs/TabGroups.ts", "code:src/tabs/TabManager.ts"], view.Hits.Where(h => h.Matched == "path").Select(h => h.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_piece_of_a_word_finds_it_through_the_trigram_fallback()
    {
        var view = (await Ask("anager")).Value;

        Assert.Contains(view.Hits, h => h.Key.Value == Manager);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_limit_cuts_the_hits_and_total_counts_every_match()
    {
        var all = (await Ask("tab")).Value;
        var two = (await Ask("tab", limit: 2)).Value;

        Assert.Equal(2, two.Hits.Count);
        Assert.Equal(all.Total, two.Total);
        Assert.True(two.Total > 2);
        Assert.Equal(all.Hits.Take(2).Select(h => h.Key.Value), two.Hits.Select(h => h.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_area_keeps_only_its_nodes_and_an_unknown_area_matches_nothing()
    {
        var all = (await Ask("tab")).Value;
        var area = all.Hits[0].Area;

        var inside = (await Ask("tab", area: area)).Value;
        var nowhere = (await Ask("tab", area: "feature:nope")).Value;

        Assert.All(inside.Hits, h => Assert.Equal(area, h.Area));
        Assert.Equal(0, nowhere.Total);
        Assert.Empty(nowhere.Hits);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ---")]
    public async Task A_question_with_no_words_is_006(string q)
    {
        var refused = await Ask(q);

        Assert.True(refused.IsError);
        Assert.Equal("SND-CONTEXT-006", refused.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(-1)]
    public async Task A_limit_outside_1_to_200_is_006(int limit)
    {
        var refused = await Ask("tab", limit);

        Assert.True(refused.IsError);
        Assert.Equal("SND-CONTEXT-006", refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_finds_only_what_its_repo_may_see()
    {
        var own = (await Ask("tab", scope: MapWorld.Session("aw-split-panes"))).Value;
        var other = (await Ask("tab", scope: MapWorld.Session("other"))).Value;

        Assert.True(own.Total > 0);
        Assert.Equal(0, other.Total);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_untrusted_hit_is_fenced_for_the_mcp_and_plain_for_the_terminal()
    {
        var terminal = (await Ask("tab manager spec", scope: MapWorld.HumanVia(ContextClient.Cli))).Value;
        var mcp = (await Ask("tab manager spec", scope: MapWorld.HumanVia(ContextClient.Mcp))).Value;

        var plain = terminal.Hits.First(static h => h.Key.Kind == Context.Core.Graph.NodeKind.Record);
        var fenced = mcp.Hits.First(static h => h.Key.Kind == Context.Core.Graph.NodeKind.Record);
        Assert.DoesNotContain("<untrusted", plain.Label, StringComparison.Ordinal);
        Assert.Contains("<untrusted", fenced.Label, StringComparison.Ordinal);
        Assert.Contains("<untrusted", fenced.Snippet, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_index_is_built_once_per_graph_and_not_per_question()
    {
        _ = await Ask("tab");
        _ = await Ask("manager");
        _ = await Ask("group");

        Assert.Equal(1, _world.SearchBuilds);
    }
}
