using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Extract.DataStores;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Tests.Extract;

/// <summary>#1365: tables declared with Drizzle give things, and the code that reads or writes them (through an alias, a barrel, a renamed or a namespace import) gets reads and writes edges.</summary>
public sealed class DrizzleDataStoreTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(DrizzleRepo.Repo);

    private static ExtractedMap Run() =>
        DataStorePass.Run(_repo, [.. DrizzleRepo.Files.Select(f => new RepoFile(f.Key, f.Value))], DataStoreAdapters.All, ContextSettings.DefaultSchemaGlobs);

    [Fact, Trait("Lane", "unit")]
    public void The_declared_tables_become_things_named_by_their_sql_name_and_the_zod_schemas_declare_none()
    {
        var things = Run().Nodes.Where(n => n.Key.Kind == NodeKind.Thing).ToList();

        Assert.Equal(
            ["thing:authors|authors", "thing:blog-posts|blog_posts", "thing:post-tags|post_tags"],
            things.Select(n => $"{n.Key.Value}|{n.Label}").Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.All(things, n => Assert.Equal(["repo:aw-notes/src/db/schema.ts"], n.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Code_that_reads_or_writes_a_table_gets_the_edge_whatever_import_form_names_it()
    {
        var edges = Run().Edges.Select(e => $"{e.From.Value} -{e.Type}-> {e.To.Value}").Order(StringComparer.Ordinal);

        Assert.Equal(
            [
                "code:src/routes/create-post.ts -Writes-> thing:blog-posts",
                "code:src/routes/list-posts.ts -Reads-> thing:authors",
                "code:src/routes/list-posts.ts -Reads-> thing:blog-posts",
                "code:src/routes/tag.ts -Reads-> thing:blog-posts",
                "code:src/routes/tag.ts -Writes-> thing:post-tags",
            ],
            edges,
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_edge_is_deterministic_and_rests_on_the_file_that_makes_it()
    {
        var map = Run();

        Assert.NotEmpty(map.Edges);
        Assert.All(map.Edges, e =>
        {
            Assert.Equal(Provenance.Deterministic, e.Provenance);
            Assert.Equal(["repo:aw-notes/" + e.From.Name], e.EvidenceOrEmpty.Select(r => r.Key.Value), StringComparer.Ordinal);
        });
    }
}
