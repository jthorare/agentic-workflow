using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel.Json;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

public sealed class ContextHistoryTextTests
{
    private static readonly DateTimeOffset _at = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);

    internal static HistoryView View()
    {
        var split = NodeKey.From("feature:layout.split-pane");
        var record = new HistoryRecord(NodeKey.From("record:linear:issue/0203"), "Ticket 0203", "linear", [split], Trust.Untrusted, CitationState.Unchecked, [], [split], _at.AddDays(-30), _at.AddDays(3), _at, Leaky: true);
        var change = new HistoryChange("commit:" + new string('a', 40), ContextRepoName.From("aw-split-panes"), new string('a', 40), _at, "Merge pull request #5 from fixture-labs/nts-130", HistoryChangeKind.Merge, "nts-130", 5,
            ["src/layout/SplitPane.ts"], [split], Trust.Untrusted);
        var gap = new SearchedSource(SourceId.From("slack"), SearchedState.Unavailable, "not-synced", "SND-CONTEXT-161", AsOf: null) { Sync = new SearchedSync("running", SyncJobId.From("JOB-1")) };
        var searched = new LensSearched(["feature:layout.split-pane"], "v2", 0, [], [gap, new SearchedSource(SourceId.From("git-history"), SearchedState.Ok, "capped", Code: null, _at)],
            new SearchedBands(new BandSearched(Empty: false), new BandSearched(Empty: false), new BandSearched(Empty: false)));
        return new HistoryView(1, [new LensAnchor(split, "Split pane", NodeKind.Feature, Trust.Trusted)], ["feature:nope"], "v2", Draft: false, _at, new HistoryWindow(Since: null, Before: _at),
            [record], [], [change], HistoryIncidents.NotConfigured, [], new HistoryCounts(1, 0, 1, 1, 0, 2), new HistoryTruncated(Records: false, Decisions: false, Changes: true, Files: false),
            [new HistoryGit(ContextRepoName.From("aw-split-panes"), "a41b07d", UpTo: null, Shallow: true, Files: 1, Changes: 1)],
            new LensCoverage(1, new CoverageIntent(0, 1), new CoverageProduct(1, 1), new CoverageCode(1, 1, "file", 0), []), searched, ["git-history: stopped"], new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_text_names_every_state_in_words_and_the_gap_with_its_job()
    {
        var text = ContextHistoryText.History(View(), fence: false);

        Assert.Contains("history of 1 anchor (feature:layout.split-pane (Split pane)) · map v2", text, StringComparison.Ordinal);
        Assert.Contains("missing anchor: feature:nope", text, StringComparison.Ordinal);
        Assert.Contains("#5  Merge pull request #5 from fixture-labs/nts-130  branch nts-130  files src/layout/SplitPane.ts", text, StringComparison.Ordinal);
        Assert.Contains("  leaky", text, StringComparison.Ordinal);
        Assert.Contains("incidents: not-configured (not-configured)", text, StringComparison.Ordinal);
        Assert.Contains("slack unavailable (not-synced, SND-CONTEXT-161) · sync running JOB-1", text, StringComparison.Ordinal);
        Assert.Contains("git-history ok (capped)", text, StringComparison.Ordinal);
        Assert.Contains("(shallow clone)", text, StringComparison.Ordinal);
        Assert.Contains("truncated: changes", text, StringComparison.Ordinal);
        Assert.Contains("degraded: git-history: stopped", text, StringComparison.Ordinal);
        Assert.Contains("2 dropped by the cut", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_human_reads_untrusted_subjects_inside_the_fence_a_session_reads_the_servers()
    {
        var human = ContextHistoryText.History(View(), fence: true);
        var session = ContextHistoryText.History(View(), fence: false);

        Assert.Contains("<untrusted", human, StringComparison.Ordinal);
        Assert.DoesNotContain("<untrusted", session, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_history_verb_refuses_a_draft_beside_a_version_and_a_call_with_no_anchor_before_any_request()
    {
        using var error = new StringWriter();
        var output = new Output(TextWriter.Null, error, AwJson.CreateOptions(SindriJsonContext.Default));
        var verbs = new ContextLensVerbs(null!, output);   // why: both refusals return before the client is touched

        var both = await verbs.Run(["history", "feature:a", "--draft", "--map-version", "v1"], TestContext.Current.CancellationToken);
        var none = await verbs.Run(["history", "--json"], TestContext.Current.CancellationToken);
        var unknownFlag = await verbs.Run(["history", "feature:a", "--no-git"], TestContext.Current.CancellationToken);

        Assert.Equal(1, both);
        Assert.Contains("SND-CONTEXT-006", error.ToString(), StringComparison.Ordinal);
        Assert.Equal((2, 2), (none, unknownFlag));   // usage errors are tool errors
    }
}
