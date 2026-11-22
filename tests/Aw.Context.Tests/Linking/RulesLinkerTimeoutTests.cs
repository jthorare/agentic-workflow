using System.Text.RegularExpressions;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Linking;

/// <summary>A rule whose match runs out of time links nothing; the other rules still link and Link never throws (#3816).</summary>
public sealed class RulesLinkerTimeoutTests
{
    private const string Path = "src/layout/SplitPane.ts";
    private static readonly RecordKey _record = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly IReadOnlySet<string> _teams = new HashSet<string>(StringComparer.Ordinal) { "SPL" };

    private static GraphSnapshot Map() => new GraphBuilder()
        .Node("feature:layout.split-pane", "Split pane")
        .Node("code:" + Path, "SplitPane.ts", Provenance.Deterministic)
        .Edge("code:" + Path, EdgeType.Implements, "feature:layout.split-pane", provenance: Provenance.Deterministic)
        .Build();

    [Fact, Trait("Lane", "unit")]
    public void A_rule_that_times_out_links_nothing_and_the_other_rules_still_link()
    {
        var timedOut = new List<LinkRule>();
        IReadOnlyList<IntentLink> links = [];

        var thrown = Record.Exception(() => links = RulesLinker.LinkWith(_record, "Implemented in " + Path + ", follow-up in SPL-104.", (Map(), [], _teams, []), (timedOut.Add, TimeOut(LinkRule.Path))));

        Assert.Null(thrown);
        Assert.Equal([LinkRule.Path], timedOut);
        Assert.DoesNotContain(links, l => l.Rule == LinkRule.Path);
        Assert.Contains(links, l => l.Rule == LinkRule.IssueKey && l.Target.Value == "record:SPL-104");
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_rule_timing_out_gives_no_links_and_names_each_rule_once()
    {
        var timedOut = new List<LinkRule>();

        var links = RulesLinker.LinkWith(_record, "Implemented in " + Path + ", follow-up in SPL-104.", (Map(), [], _teams, []), (timedOut.Add, static _ => throw new RegexMatchTimeoutException()));

        Assert.Empty(links);
        Assert.Equal(Enum.GetValues<LinkRule>().Order(), timedOut.Order());
    }

    private static Action<LinkRule> TimeOut(LinkRule which) => rule =>
    {
        if (rule == which)
        {
            throw new RegexMatchTimeoutException();
        }
    };
}
