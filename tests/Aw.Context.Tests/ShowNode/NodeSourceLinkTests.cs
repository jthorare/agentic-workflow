using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.ShowNode;

/// <summary>The source of a node in the context panel links to Linear, GitHub or the file in the repo (issue #1702).</summary>
public sealed class NodeSourceLinkTests : IDisposable
{
    private const string Repo = "aw-split-panes";
    private const string Ticket = "linear:issue/0e5a1b00-0000-4000-8000-000000000a01";
    private const string Quoted = "linear:issue/0e5a1b00-0000-4000-8000-000000000a02";
    private const string Issue = "github:issue/acme/aw-split-panes/7";
    private const string Quote = "keep the pane in view";
    private const string LinearUrl = "https://linear.app/acme/issue/ENG-12/split-panes";
    private const string QuotedUrl = "https://linear.app/acme/issue/ENG-13/quoted";
    private const string IssueUrl = "https://github.com/acme/aw-split-panes/issues/7";
    private const string Blob = "https://github.com/acme/aw-split-panes/blob/";
    private const string SshRemote = "git@github.com:acme/aw-split-panes.git";
    private const string Leak = "SEC" + "RET";

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static string DetailMap(string featureEvidence = "repo:aw-split-panes/docs/a.md", string codeKey = "code:src/a.ts") => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        $"  - {{key: \"feature:a\", label: \"Alpha\", evidence: [{{key: \"{featureEvidence}\"}}]}}",
        "  - {key: \"feature:plain\", label: \"Plain\"}",
        $"  - {{key: \"{codeKey}\", label: \"a.ts\", provenance: deterministic}}",
        $"  - {{key: \"record:{Ticket}\", label: \"Ticket title\"}}",
        $"  - {{key: \"record:{Quoted}\", label: \"Quoted ticket\"}}",
        $"  - {{key: \"record:{Issue}\", label: \"Issue title\"}}",
        "edges:",
        $"  - {{from: \"{codeKey}\", to: \"feature:a\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/a.ts\"}}]}}",
        $"  - {{from: \"record:{Ticket}\", to: \"feature:a\", type: mentions}}",
        $"  - {{from: \"{DecisionRow.KeyFor(RecordKey.From(Quoted), Quote).Value}\", to: \"feature:a\", type: decides}}",
        "decisions:",
        $"  - {{record: \"{Quoted}\", quote: \"{Quote}\", wording: \"Decided.\", at: 2026-07-15T10:00:00Z}}");

    private async Task Store(string key, string? url, SourceId? source = null)
    {
        var record = RecordSamples.Record(key, source: source) with { Url = url };
        var committed = await _world.Records.Commit(RecordSamples.Batch(record.Source, 1, RecordSamples.Write(record, "text of " + key)), MapWorld.None);
        Assert.True(committed.IsSuccess, committed.IsError ? committed.Error.Message : null);
    }

    private async Task<NodeSource?> SourceOf(string key) => (await _world.Engine.ShowNode(key, MapWorld.Human, MapWorld.None)).Value.Node.Source;

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_node_links_to_the_url_its_stored_record_carries()
    {
        _ = await _world.ImportYaml(DetailMap());
        await Store(Ticket, LinearUrl);

        var source = await SourceOf("record:" + Ticket);

        Assert.Equal(new NodeSource("ticket", Ticket, Title: null, Url: LinearUrl), source);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_feature_and_a_decision_link_to_the_record_they_name()
    {
        _ = await _world.ImportYaml(DetailMap(featureEvidence: Ticket));
        await Store(Ticket, LinearUrl);
        await Store(Quoted, QuotedUrl);

        var feature = await SourceOf("feature:a");
        var decision = await SourceOf(DecisionRow.KeyFor(RecordKey.From(Quoted), Quote).Value);

        Assert.Equal(new NodeSource("ticket", Ticket, "Ticket title", LinearUrl), feature);
        Assert.Equal(new NodeSource("ticket", Quoted, "Quoted ticket", QuotedUrl), decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_github_issue_record_links_to_its_issue()
    {
        _ = await _world.ImportYaml(DetailMap());
        await Store(Issue, IssueUrl, RecordSamples.Github);

        var source = await SourceOf("record:" + Issue);

        Assert.Equal(IssueUrl, source!.Url);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("https://evil.example/x")]
    [InlineData("http://linear.app/acme/issue/ENG-12/split-panes")]
    public async Task A_stored_url_that_is_not_an_allowed_permalink_is_not_served(string stored)
    {
        _ = await _world.ImportYaml(DetailMap());
        await Store(Ticket, stored);
        await Store(Quoted, QuotedUrl);

        var refused = await SourceOf("record:" + Ticket);
        var allowed = await SourceOf(DecisionRow.KeyFor(RecordKey.From(Quoted), Quote).Value);

        Assert.Null(refused!.Url);
        Assert.Equal(QuotedUrl, allowed!.Url);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(SshRemote)]
    [InlineData("https://x-access-token:" + Leak + "@github.com/acme/aw-split-panes.git")]
    [InlineData("https://github.com/acme/aw-split-panes")]
    public async Task A_repo_doc_links_to_its_file_on_github_at_HEAD_for_an_imported_map_and_never_carries_credentials(string remote)
    {
        _ = _world.Remotes.Set(ContextRepoName.From(Repo), remote);
        _ = await _world.ImportYaml(DetailMap());

        var source = await SourceOf("feature:a");

        Assert.Equal(new NodeSource("doc", "repo:aw-split-panes/docs/a.md", Title: null, Url: Blob + "HEAD/docs/a.md"), source);
        Assert.DoesNotContain(Leak, source!.Url, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_code_node_links_to_its_file_on_the_map_s_repo()
    {
        _ = _world.Remotes.Set(ContextRepoName.From(Repo), SshRemote);
        _ = await _world.ImportYaml(DetailMap());

        var source = await SourceOf("code:src/a.ts");

        Assert.Equal(new NodeSource("file", "src/a.ts", Title: null, Url: Blob + "HEAD/src/a.ts"), source);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_path_with_a_space_or_a_hash_is_percent_escaped_in_the_link()
    {
        _ = _world.Remotes.Set(ContextRepoName.From(Repo), SshRemote);
        _ = await _world.ImportYaml(DetailMap(codeKey: "code:docs/a b#c.md"));

        var source = await SourceOf("code:docs/a b#c.md");

        Assert.Equal(Blob + "HEAD/docs/a%20b%23c.md", source!.Url);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_built_map_pins_a_file_link_to_the_commit_it_was_built_at()
    {
        _ = _world.Remotes.Set(ContextRepoName.From(Repo), SshRemote);
        _ = await Drafts.Build(_world);
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), Drafts.Scope, MapWorld.None)).IsSuccess);
        Assert.True((await _world.Map.Promote(Drafts.Scope, MapWorld.None)).IsSuccess);

        var node = (await _world.Engine.ShowNode("code:src/commands/ids.ts", Drafts.Scope, MapWorld.None)).Value.Node;

        Assert.Equal(Blob + MapWorld.Head + "/src/commands/ids.ts", node.Source!.Url);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_an_origin_or_with_one_that_is_not_github_there_is_no_file_link_but_a_record_link_stays()
    {
        _ = await _world.ImportYaml(DetailMap());
        await Store(Ticket, LinearUrl);

        var noRemote = await SourceOf("code:src/a.ts");
        _ = _world.Remotes.Set(ContextRepoName.From(Repo), "git@gitlab.com:acme/x.git");
        var gitlab = await SourceOf("feature:a");
        var record = await SourceOf("record:" + Ticket);

        Assert.Null(noRemote!.Url);
        Assert.Null(gitlab!.Url);
        Assert.Equal(LinearUrl, record!.Url);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_checkout_still_shows_the_node_without_a_file_link_and_a_record_link_stays()
    {
        _ = _world.Remotes.Unreadable(ContextRepoName.From(Repo));
        _ = await _world.ImportYaml(DetailMap());
        await Store(Ticket, LinearUrl);

        var file = await _world.Engine.ShowNode("code:src/a.ts", MapWorld.Human, MapWorld.None);
        var record = await SourceOf("record:" + Ticket);

        Assert.True(file.IsSuccess);
        Assert.Null(file.Value.Node.Source!.Url);
        Assert.Equal(LinearUrl, record!.Url);
    }
}
