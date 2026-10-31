using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Linking;

/// <summary>The linker reads a bounded prefix of a record's text, cut at a line or word edge (#1769).</summary>
public sealed class RulesLinkerBoundTests
{
    private const int Max = RulesLinker.MaxLinkedChars;
    private const string Path = "src/layout/SplitPane.ts";
    private static readonly RecordKey _record = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");

    private static GraphSnapshot Map() => new GraphBuilder()
        .Node("feature:layout.split-pane", "Split pane")
        .Node("code:" + Path, "SplitPane.ts", Provenance.Deterministic)
        .Edge("code:" + Path, EdgeType.Implements, "feature:layout.split-pane", provenance: Provenance.Deterministic)
        .Build();

    private static IReadOnlyList<IntentLink> Link(string text) =>
        RulesLinker.Link(_record, text, Map(), ([], new HashSet<string>(StringComparer.Ordinal)), []);

    [Fact, Trait("Lane", "unit")]
    public void Text_at_or_under_the_limit_is_unchanged()
    {
        var text = new string('a', Max);

        Assert.Same(text, RulesLinker.Bounded(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void Longer_text_is_cut_at_the_last_newline_before_the_limit()
    {
        var text = new string('a', Max - 100) + "\n" + new string('b', Max);

        Assert.Equal(new string('a', Max - 100), RulesLinker.Bounded(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_newline_the_cut_is_at_the_last_whitespace()
    {
        var text = new string('a', Max - 50) + " " + new string('b', 20) + "\t" + new string('c', Max);

        Assert.Equal(new string('a', Max - 50) + " " + new string('b', 20), RulesLinker.Bounded(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_edge_far_before_the_limit_is_ignored_and_the_cut_is_the_limit()
    {
        var text = new string('a', 100) + "\n" + new string('b', Max);

        Assert.Equal(Max, RulesLinker.Bounded(text).Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_whitespace_the_cut_never_splits_a_surrogate_pair()
    {
        var text = new string('a', Max - 1) + "😀" + "tail";

        var bounded = RulesLinker.Bounded(text);

        Assert.Equal(Max - 1, bounded.Length);
        Assert.False(char.IsHighSurrogate(bounded[^1]));
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_whitespace_and_no_pair_the_cut_is_the_limit() => Assert.Equal(Max, RulesLinker.Bounded(new string('a', Max + 5)).Length);

    [Fact, Trait("Lane", "unit")]
    public void A_repo_path_straddling_the_limit_is_not_linked_but_the_same_path_inside_it_is()
    {
        var straddling = new string('x', Max - 10) + " " + Path;   // the path starts before the mark and ends after it
        var inside = Path + " " + new string('x', Max);

        Assert.Empty(Link(straddling));
        Assert.Contains(Link(inside), l => l.Rule == LinkRule.Path);
    }
}
