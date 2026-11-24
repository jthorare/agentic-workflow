using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>
/// Issue 3696: the concepts answer lists the repos the map holds nodes of, so a page that opens on the concept cards shows the repo picker without reading the whole map. The
/// list comes from the map the caller may read, never from the graph cut to the asked repo: after a pick it still lists every repo, so the picker stays.
/// </summary>
[Trait("Lane", "unit")]
public sealed class ConceptReposTests : IDisposable
{
    private static readonly ContextScope _app = MapWorld.HumanVia(ContextClient.Http);

    private static readonly WholeMapRepo[] _both = [new("aw-split-panes", 2), new("other", 2)];

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    // Two features of one repo each and one shared by both, as in the whole map's two-repo test.
    private Task<MapImported> TwoRepos() => _world.ImportYaml(MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"feature:web\", label: \"Web\"}", "  - {key: \"feature:api\", label: \"Api\"}", "  - {key: \"feature:shared\", label: \"Shared\"}",
        "  - {key: \"code:w.ts\", label: \"w.ts\"}", "  - {key: \"code:a.ts\", label: \"a.ts\"}",
        "edges:",
        "  - {from: \"code:w.ts\", to: \"feature:web\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/w.ts\"}]}",
        "  - {from: \"code:a.ts\", to: \"feature:api\", type: implements, provenance: deterministic, evidence: [{key: \"repo:other/a.ts\"}]}",
        "  - {from: \"code:w.ts\", to: \"feature:shared\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/w.ts\"}]}",
        "  - {from: \"code:a.ts\", to: \"feature:shared\", type: implements, provenance: deterministic, evidence: [{key: \"repo:other/a.ts\"}]}"));

    private async Task<ConceptsView> Ask(string? repo = null, string? q = null)
    {
        var read = await _world.Engine.Concepts(new ConceptsQuery(MapRead.Current, new ConceptPage(), repo, q), _app, MapWorld.None);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        return read.Value;
    }

    [Fact]
    public async Task The_concepts_answer_of_a_map_over_two_repos_lists_both_with_their_node_counts()
    {
        _ = await TwoRepos();

        var view = await Ask();

        Assert.Equal(_both, view.Repos);
    }

    [Fact]
    public async Task Asking_for_one_repo_still_lists_both_so_the_picker_stays()
    {
        _ = await TwoRepos();

        var view = await Ask(repo: "other");

        Assert.Equal("other", view.Repo);
        Assert.Equal(_both, view.Repos);
    }

    [Fact]
    public async Task A_search_built_fresh_lists_both_repos_too()
    {
        _ = await TwoRepos();

        var view = await Ask(repo: "aw-split-panes", q: "web");

        Assert.Equal(_both, view.Repos);
    }
}
