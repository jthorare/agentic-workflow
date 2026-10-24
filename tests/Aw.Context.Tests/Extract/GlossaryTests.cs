using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Extract;

public sealed class GlossaryTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private const string Context = """
        # Project context

        ## Glossary

        - **Core / On Call** (aliases: on call, oncall) - the rota a team keeps for urgent work
        - **Shift Times**: when a shift starts and ends
        **Coverage Pattern** (aka cover) - which roles must be present

        ## Other

        - **Not A Term** - outside the glossary section
        """;

    private static ExtractedMap Read(string text, string path = "CONTEXT.md") => MapExtractor.Extract(_repo, [new RepoFile(path, text)]);

    [Fact, Trait("Lane", "unit")]
    public void Each_term_under_the_glossary_heading_becomes_a_deterministic_node_implemented_by_its_file()
    {
        var map = Read(Context);

        var concepts = map.Nodes.Where(n => n.Key.Value.StartsWith("feature:glossary/", StringComparison.Ordinal)).ToList();
        Assert.Equal(["feature:glossary/core-on-call", "feature:glossary/coverage-pattern", "feature:glossary/shift-times"], concepts.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.All(concepts, n => Assert.Equal(Provenance.Deterministic, n.Provenance));
        Assert.Equal(3, map.Edges.Count(e => e.Type == EdgeType.Implements && e.From.Value == "code:CONTEXT.md"));
        Assert.DoesNotContain(map.Nodes, n => n.Key.Value.Contains("not-a-term", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Aliases_become_alias_keys_of_the_term()
    {
        var map = Read(Context);

        Assert.Equal(
            [("feature:glossary/cover", "feature:glossary/coverage-pattern"), ("feature:glossary/on-call", "feature:glossary/core-on-call"), ("feature:glossary/oncall", "feature:glossary/core-on-call")],
            map.Aliases.Select(a => (a.Alias.Value, a.Target.Value)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_glossary_file_lists_every_term_and_other_markdown_is_not_read()
    {
        Assert.Single(Read("- **Pane** - a region", "docs/GLOSSARY.md").Nodes, n => n.Key.Kind == NodeKind.Feature);
        Assert.Empty(Read("- **Pane** - a region", "README.md").Nodes);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Core / On Call", "feature:glossary/core-on-call")]
    [InlineData("core/on-call", "feature:glossary/core-on-call")]
    [InlineData("shift times", "feature:glossary/shift-times")]
    [InlineData("SHIFT  TIMES", "feature:glossary/shift-times")]
    [InlineData("on call", "feature:glossary/core-on-call")]
    [InlineData("Cover", "feature:glossary/coverage-pattern")]
    public void A_phrase_resolves_to_the_concept_without_an_exact_key(string phrase, string expected)
    {
        var map = Read(Context);
        var graph = new GraphSnapshot(map.Nodes, map.Edges, map.Aliases, []);

        Assert.Equal(expected, AnchorMatch.Resolve(graph, phrase).Value.Key.Value);
    }
}
