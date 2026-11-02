using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Features.BuildMap;

namespace Aw.Context.Tests.BuildMap;

public sealed class DocRecordsTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("app");
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-20T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly string[] _names = ["AGENTS.md", "README.md", "CONTEXT.md"];

    internal static readonly string[] Paths =
    [
        "src/billing/AGENTS.md", "src/billing/Invoice.cs", "src/billing/Tax.cs",
        "src/links/AGENTS.md", "src/links/Share.cs", "src/links/Token.cs",
        "docs/guide.md", "notes/random.md", "README.markdown",
    ];

    private static IReadOnlyList<GraphNode> Find(IReadOnlyList<string>? paths = null, IReadOnlyList<string>? names = null) =>
        DocRecords.Find(_repo, paths ?? Paths, ContextSettings.DefaultDocGlobs, names ?? _names, _at);

    [Fact, Trait("Lane", "unit")]
    public void A_doc_matching_a_glob_or_a_directory_name_is_a_record_and_nothing_else_is()
    {
        var records = Find();

        Assert.Equal(
            ["record:repo:app/docs/guide.md", "record:repo:app/src/billing/AGENTS.md", "record:repo:app/src/links/AGENTS.md"],
            records.Select(r => r.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.DoesNotContain(records, r => r.Key.Value.Contains("notes/random.md", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_is_keyed_like_the_overlays_labelled_by_path_deterministic_and_dated_at_the_head_commit()
    {
        var record = Assert.Single(Find(), r => r.Label == "docs/guide.md");

        Assert.Equal("record:" + RepoDocs.Key(_repo, "docs/guide.md").Value, record.Key.Value);
        Assert.Equal(NodeKind.Record, record.Key.Kind);
        Assert.Equal((Provenance.Deterministic, 1.0, _at), (record.Provenance, record.Confidence, record.At));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_directory_names_are_a_setting_and_an_empty_list_leaves_only_the_globs()
    {
        var onlyGuide = Find(names: ["GUIDE.md"], paths: [.. Paths, "tools/GUIDE.md"]);
        var none = Find(names: []);

        Assert.Equal(["record:repo:app/docs/guide.md", "record:repo:app/tools/GUIDE.md"], onlyGuide.Select(r => r.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["record:repo:app/docs/guide.md"], none.Select(r => r.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Running_twice_gives_the_same_records_whatever_the_path_order()
    {
        var first = Find();
        var second = Find([.. Paths.Reverse()]);

        Assert.Equal(first.Select(r => r.Key.Value), second.Select(r => r.Key.Value), StringComparer.Ordinal);
        Assert.Equal(first, second);
    }
}
