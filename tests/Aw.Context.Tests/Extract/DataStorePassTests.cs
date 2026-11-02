using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Extract.DataStores;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Tests.Extract;

public sealed class DataStorePassTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(DataStoreRepo.Repo);

    private static ExtractedMap Run(IEnumerable<KeyValuePair<string, string>>? files = null) =>
        DataStorePass.Run(_repo, [.. (files ?? DataStoreRepo.Files).Select(f => new RepoFile(f.Key, f.Value))], [new GenericSqlAdapter()], ContextSettings.DefaultSchemaGlobs);

    private static string[] Edges(ExtractedMap map) => [.. map.Edges.Select(e => $"{e.From.Value} -{e.Type}-> {e.To.Value}")];

    [Fact, Trait("Lane", "unit")]
    public void The_declared_tables_become_deterministic_things_resting_on_their_schema_files()
    {
        var things = Run().Nodes.Where(n => n.Key.Kind == NodeKind.Thing).ToList();

        Assert.Equal(
            [
                "thing:audit-event|audit_event|repo:aw-split-panes/db/schema/001_core.sql",
                "thing:invoice-line|invoice_line|repo:aw-split-panes/db/schema/002_billing.sql",
                "thing:shared-link|shared_link|repo:aw-split-panes/db/schema/001_core.sql",
            ],
            things.Select(n => $"{n.Key.Value}|{n.Label}|{string.Join(',', n.EvidenceOrEmpty.Select(e => e.Key.Value))}"),
            StringComparer.Ordinal);
        Assert.All(things, n => Assert.Equal(Provenance.Deterministic, n.Provenance));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_reads_or_writes_a_table_gets_the_edge_and_one_that_only_names_it_gets_none()
    {
        Assert.Equal(
            [
                "code:src/billing/lines.ts -Reads-> thing:invoice-line",
                "code:src/billing/lines.ts -Writes-> thing:invoice-line",
                "code:src/links/read.ts -Reads-> thing:shared-link",
                "code:src/links/write.ts -Writes-> thing:audit-event",
            ],
            Edges(Run()),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_edge_is_deterministic_and_rests_on_the_file_that_makes_it()
    {
        var map = Run();

        Assert.All(map.Edges, e =>
        {
            Assert.Equal(Provenance.Deterministic, e.Provenance);
            Assert.Equal(["repo:aw-split-panes/" + e.From.Name], e.EvidenceOrEmpty.Select(r => r.Key.Value), StringComparer.Ordinal);
        });
        Assert.Equal(
            ["code:src/billing/lines.ts", "code:src/links/read.ts", "code:src/links/write.ts"],
            map.Nodes.Where(n => n.Key.Kind == NodeKind.Code).Select(n => n.Key.Value),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_edge_follows_the_edge_rules_and_has_both_ends_among_the_nodes()
    {
        var map = Run();
        var keys = map.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(map.Edges);
        Assert.All(map.Edges, e =>
        {
            Assert.True(EdgeRules.Allows(e.Type, e.From.Kind, e.To.Kind), $"{e.From.Value} {e.Type} {e.To.Value}");
            Assert.Contains(e.From.Value, keys);
            Assert.Contains(e.To.Value, keys);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_files_in_any_order_give_the_same_map()
    {
        var forward = Run();
        var backward = Run(DataStoreRepo.Files.Reverse());

        Assert.Equal(Edges(forward), Edges(backward), StringComparer.Ordinal);
        Assert.Equal(forward.Nodes.Select(n => n.Key.Value), backward.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_schema_file_means_no_things_and_no_edges()
    {
        var map = Run(DataStoreRepo.Files.Where(f => !f.Key.EndsWith(".sql", StringComparison.Ordinal)));

        Assert.Empty(map.Nodes);
        Assert.Empty(map.Edges);
    }
}
