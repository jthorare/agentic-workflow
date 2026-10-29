using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The context goldens (Sindri's own, tests/Aw.Sindri.Tests/Golden/Api/context-*) through the app's records: every literal is read from a golden, never typed.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextGoldenTests
{
    private static readonly string[] _slugs =
        ["context-lenses", "context-filemap", "context-map-review", "context-map-review-history", "context-map", "context-map-publish", "context-node", "context-map-promote", "context-code"];

    private static T Read<T>(string slug, string scenario)
        where T : class => Read<T>(GoldenFiles.Api(slug, scenario));

    private static T Read<T>(string body)
        where T : class
    {
        var read = AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(body), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        return read.Value;
    }

    private static bool IsRefusal(string body) => JsonNode.Parse(body) is JsonObject json && json["traceId"] is not null;

    // What each served success golden is, by route and scenario; a scenario this table does not place fails, so a new golden is placed here.
    private static Func<string, object> As(string slug, string scenario) => slug switch
    {
        "context-lenses" => Read<LensesView>,
        "context-filemap" => Read<FileMapView>,
        "context-code" => Read<CodeView>,
        "context-map-review" => scenario.StartsWith("post-", StringComparison.Ordinal) || scenario.StartsWith("session-", StringComparison.Ordinal) ? Read<ReviewDecisionResponse> : Read<ReviewQueueView>,
        "context-map-review-history" => Read<ReviewHistoryView>,
        "context-map" => Read<MapStatusView>,
        "context-map-publish" => Read<MapPublishView>,
        "context-node" => Read<NodeView>,
        "context-map-promote" => Read<PromotePreview>,
        _ => throw new InvalidOperationException(slug),
    };

    [Fact]
    public void Every_context_golden_reads_through_the_apps_json_context()
    {
        var goldens = GoldenFiles.All().Where(g => g.Kind == "Api" && _slugs.Contains(g.Slug, StringComparer.Ordinal)).ToList();
        Assert.Contains(goldens, g => g.Slug == "context-lenses" && g.Scenario == "set-two-anchors");

        foreach (var (_, slug, scenario, body) in goldens)
        {
            if (IsRefusal(body))
            {
                var problem = Read<ProblemView>(body);
                Assert.StartsWith("SND-", problem.Title, StringComparison.Ordinal);
                Assert.False(string.IsNullOrEmpty(problem.Detail), $"{slug}/{scenario}");
                continue;
            }

            _ = As(slug, scenario)(body);
        }
    }

    // Q11: a served word is a string, so a word added later reads and shows raw; the words this build knows are listed by ContextWords.
    [Fact]
    public void Enums_and_word_sets_read_by_their_wire_names()
    {
        var queue = Read<ReviewQueueView>("context-map-review", "map-tabs-draft");
        var lenses = Read<LensesView>("context-lenses", "map-tabs");
        var tabs = Read<FileMapView>("context-filemap", "src-tabs");
        var tree = Read<FileMapView>("context-filemap", "root");

        Assert.All(queue.Entries, e => Assert.True(ContextWords.Known(ContextWords.Groups, e.Group), e.Group));
        Assert.All(tabs.Entries.Concat(tree.Entries), e => Assert.True(ContextWords.Known(ContextWords.FileStatuses, e.Status) && ContextWords.Known(ContextWords.EntryTypes, e.Type), e.Path));
        Assert.All(lenses.Conflicts, c => Assert.True(ContextWords.Known(ContextWords.ConflictKinds, c.Kind), c.Kind));
        Assert.True(ContextWords.Known(ContextWords.SymbolStates, lenses.Bands.Code.Symbols.State));
        Assert.All(lenses.Bands.Product.Own.Select(n => n.Trust).Concat(lenses.Bands.Intent.Records.Select(r => r.Trust)), t => Assert.True(ContextWords.Known(ContextWords.Trusts, t), t));
        Assert.All(lenses.Searched.Sources, s => Assert.True(ContextWords.Known(ContextWords.SearchedStates, s.State), s.State));
        Assert.Contains(lenses.Searched.Sources, s => s.State == "unavailable");
        Assert.False(ContextWords.Known(ContextWords.Groups, "future-group"));
        Assert.False(ContextWords.Known(ContextWords.Groups, word: null));
    }

    [Fact]
    public void An_unknown_future_word_reads_without_throwing()
    {
        var body = GoldenFiles.Api("context-map-review", "map-tabs-draft").Replace("\"group\": \"model\"", "\"group\": \"future-group\"", StringComparison.Ordinal);

        var queue = Read<ReviewQueueView>(body);

        Assert.Contains(queue.Entries, e => e.Group == "future-group");
        Assert.False(ContextWords.Known(ContextWords.Groups, "future-group"));
    }

    [Fact]
    public void A_single_anchor_answer_reads_as_a_one_element_set()
    {
        var one = Read<LensesView>("context-lenses", "map-tabs");
        var two = Read<LensesView>("context-lenses", "set-two-anchors");

        Assert.Equal([(string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!], one.Anchors.Select(a => a.Key), StringComparer.Ordinal);
        Assert.Equal(GoldenFiles.Node("context-lenses", "set-two-anchors")["anchors"]!.AsArray().Select(a => (string)a!["key"]!), two.Anchors.Select(a => a.Key), StringComparer.Ordinal);
        Assert.Equal((1, 2), (one.SchemaVersion, two.SchemaVersion));
    }

    [Fact]
    public void Item_membership_comes_from_the_list_it_is_in()
    {
        // why: the golden's session sees its ancestors as its own nodes, so it serves no neighbor; one is added to read a reach's path
        var node = JsonNode.Parse(GoldenFiles.Api("context-lenses", "session-set-other-repo"))!;
        var served = node["bands"]!["product"]!;
        var neighbor = served["own"]![0]!.DeepClone();
        neighbor["depth"] = 1;
        neighbor["path"] = new JsonArray("part-of feature:tabs.tab-manager");
        served["neighbors"]!.AsArray().Add(neighbor);
        var product = Read<LensesView>(node.ToJsonString()).Bands.Product;

        Assert.Equal(served["own"]!.AsArray().Count, product.Own.Count);
        Assert.Equal(served["neighbors"]!.AsArray().Count, product.Neighbors.Count);
        Assert.Equal(served["affects"]!.AsArray().Count, product.Affects.Count);
        Assert.Equal(["part-of feature:tabs.tab-manager"], product.Neighbors[0].Path, StringComparer.Ordinal);
    }

    [Fact]
    public void A_missing_mapCommit_reads_as_null()
    {
        var node = JsonNode.Parse(GoldenFiles.Api("context-lenses", "map-tabs"))!;
        Assert.Null(node["mapCommit"]);
        Assert.Null(Read<LensesView>("context-lenses", "map-tabs").MapCommit);

        node["mapCommit"] = "7c3e1a2";

        Assert.Equal("7c3e1a2", Read<LensesView>(node.ToJsonString()).MapCommit);
    }

    [Fact]
    public void Coverage_reads_by_anchor_for_a_set()
    {
        var served = GoldenFiles.Node("context-lenses", "set-two-anchors")["coverage"]!["byAnchor"]!.AsObject();
        var coverage = Read<LensesView>("context-lenses", "set-two-anchors").Coverage;

        Assert.Equal(served.Select(p => p.Key), coverage.ByAnchor!.Keys, StringComparer.Ordinal);
        Assert.All(served, p => Assert.Equal((int)p.Value!["scope"]!, coverage.ByAnchor![p.Key].Scope));
        Assert.Null(Read<LensesView>("context-lenses", "map-tabs").Coverage.ByAnchor);
    }
}
