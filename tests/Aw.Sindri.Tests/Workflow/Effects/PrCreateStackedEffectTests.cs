using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.create-stacked' (docs-sync spec §7.2): pushes 'docs/{source head branch}' from the workspace, then opens the documentation pull request with the source head branch as its
/// base, or updates the open one for that docs branch. It replies 'pr.stacked' with 'opened' or 'updated' and 'fact:docsPr'; a push that doesn't land, or a forge that refuses, replies 'rejected'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrCreateStackedEffectTests
{
    private const string Workspace = "/home/wt/docs";
    private const string Link = "https://github.com/acme/web/pull/7";
    private const string SourceHead = "1111111111111111111111111111111111111111";
    private const string DocsHead = "c1";
    private const string DocsBranch = "docs/sindri/l1/p1";
    private const string Marker = "sindri.docs:T-4-docs-sync-1:7";
    private static readonly PullRepo _repo = new("github.com", "acme", "web");

    private readonly FakePullRequests _pulls = new(["github.com"]);
    private readonly FakeRunDiff _diff = new();
    private readonly InMemoryFactStore _facts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public PrCreateStackedEffectTests() => _diff.Add(Workspace, DocsHead);

    [Fact]
    public async Task A_docs_branch_with_no_pull_request_is_pushed_without_a_lease_and_opened_on_the_source_head_branch()
    {
        var done = await Dispatch();

        Assert.Equal("opened", done.Reply?.Value);
        Assert.Equal(["Find", "PushBranch", "Open"], _pulls.Calls.ToArray());
        var pull = Assert.Single(_pulls.Pulls);
        Assert.Equal((DocsBranch, DocsHead, Marker, "docs: sync for the source pull request", false), (pull.Head, pull.HeadSha, pull.Marker, pull.Title, pull.Draft));
        Assert.Contains(SourceHead, pull.Body, StringComparison.Ordinal);
        Assert.Contains(Link, pull.Body, StringComparison.Ordinal);
        Assert.Equal(pull.Link, Content(done, "fact:docsPr"));
    }

    [Fact]
    public async Task The_docs_pull_request_is_based_on_the_source_head_branch()
    {
        _ = await Dispatch();

        Assert.Equal("sindri/l1/p1", Assert.Single(_pulls.Pulls).Base);
    }

    [Fact]
    public async Task A_docs_pull_request_already_open_is_updated_after_a_push_leased_on_its_head()
    {
        const string OldSha = "fedcba9876543210fedcba9876543210fedcba98";
        var seeded = await Seed(OldSha);

        var done = await Dispatch();

        Assert.Equal("updated", done.Reply?.Value);
        Assert.Equal(["PushBranch", "Open", "Find", "Get", "PushBranch", "Update"], _pulls.Calls.ToArray());
        Assert.Equal(seeded.Link, Content(done, "fact:docsPr"));
        Assert.Contains("aw-marker:" + Marker, Assert.Single(_pulls.Pulls).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_docs_branch_someone_else_moved_replies_rejected_and_opens_nothing()
    {
        _ = await Seed("fedcba9876543210fedcba9876543210fedcba98");
        _ = await _pulls.PushBranch(new PullBranchPush(_repo, Workspace, DocsBranch, "9999999999999999999999999999999999999999", "fedcba9876543210fedcba9876543210fedcba98"), Token);

        var done = await Dispatch();

        Assert.Equal("rejected", done.Reply?.Value);
        Assert.Null(done.Facts);
        Assert.Single(_pulls.Pulls);
    }

    [Fact]
    public async Task A_host_outside_the_forge_hosts_replies_rejected()
    {
        var done = await Dispatch(new FakePullRequests(["gitlab.example"]));

        Assert.Equal("rejected", done.Reply?.Value);
        Assert.Null(done.Facts);
    }

    [Fact]
    public async Task A_source_pull_request_state_without_a_head_branch_fails_the_effect_and_names_it()
    {
        var result = await Effect(_pulls).Dispatch(Context(onJson: """{ "head": {} }"""), Token);

        Assert.True(result.IsError);
        Assert.Contains("head", result.Error.Message, StringComparison.Ordinal);
    }

    private async Task<OpenedPull> Seed(string sha)
    {
        _ = await _pulls.PushBranch(new PullBranchPush(_repo, Workspace, DocsBranch, sha, LeaseSha: null), Token);
        var opened = await _pulls.Open(new OpenPull(_repo, DocsBranch, "sindri/l1/p1", sha, "Old", "Old body", Draft: false, Marker), Token);
        return opened.Value;
    }

    private PrCreateStackedEffect Effect(FakePullRequests pulls) => new(pulls, _diff, _facts, _facts);

    private async Task<EffectDone> Dispatch(FakePullRequests? pulls = null)
    {
        var done = await Effect(pulls ?? _pulls).Dispatch(Context(), Token);
        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        return done.Value;
    }

    private string Content(EffectDone done, string key) => Encoding.UTF8.GetString(_facts.Stored[done.Facts![FactKey.From(key)].Sha256]);

    private EffectContext Context(string? onJson = null)
    {
        var state = onJson ?? $$"""{ "ref": "{{Link}}", "url": "{{Link}}", "head": { "ref": "sindri/l1/p1", "sha": "{{SourceHead}}" }, "base": { "ref": "main" } }""";
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["pr"] = Stored(Link),
            ["on"] = Stored(state),
            ["workspace"] = Stored(Workspace),
            ["title"] = JsonSerializer.SerializeToElement("docs: sync for the source pull request"),
        };
        return new EffectContext(BlockId.From("effect.pr.create-stacked"), "T-4/docs-sync/1:5:0", "C-1", FromEngine: false, "T-4/docs-sync/1", "T-4", Attempt: 1,
            arguments, new Dictionary<FactKey, JsonElement>(), Text: null);
    }

    private JsonElement Stored(string content)
    {
        var stored = _facts.Put(Encoding.UTF8.GetBytes(content), Token).AsTask().GetAwaiter().GetResult().Value;
        using var fact = JsonDocument.Parse($$"""{"ref":"{{stored.Ref}}","sha256":"{{stored.Sha256}}","headSha":null}""");
        return fact.RootElement.Clone();
    }
}
