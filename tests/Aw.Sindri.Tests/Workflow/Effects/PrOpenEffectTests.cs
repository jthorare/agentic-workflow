using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.open' (line spec §7.5, PB spec BD1): opens the phase's pull request from the frozen hand-off (pr.json and body.md), or updates the one already open for that
/// head branch, and replies 'pr.opened' with 'fact:pr'. A forge that refuses answers 'refused'; any other forge failure is the effect's failure.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrOpenEffectTests
{
    private const string Dir = "/home/run/handoff";
    private const string Workspace = "/home/wt/phase-1";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly PullRepo _repo = new("github.com", "acme", "web");

    private readonly FakePullRequests _pulls = new(["github.com"]);
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly InMemoryPacketFiles _files = new InMemoryPacketFiles()
        .Put(Dir, "pr.json", $$"""{ "schemaVersion": 1, "base": "main", "headBranch": "sindri/l1/p1", "headSha": "{{Sha}}", "stackParent": null, "title": "Add login", "labels": ["feature"] }""")
        .Put(Dir, "body.md", "Evidence pack body");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_phase_with_no_open_pull_request_opens_one_and_replies_opened_with_fact_pr()
    {
        var done = await Dispatch();

        Assert.Equal("opened", done.Reply?.Value);
        var open = Assert.Single(_pulls.Opened);
        Assert.Equal((_repo, "sindri/l1/p1", "main", Sha, "Add login", "Evidence pack body", false), (open.Repo, open.Head, open.Base, open.HeadSha, open.Title, open.Body, open.Draft));
        Assert.Equal("sindri.pr:sindri-l1-p1", open.Marker);
        var pr = JsonDocument.Parse(Content(done, "fact:pr")).RootElement;
        Assert.Equal(("https://github.com/acme/web/pull/1", 1, "open"), (pr.GetProperty("link").GetString(), pr.GetProperty("number").GetInt32(), pr.GetProperty("state").GetString()));
        Assert.Equal(("main", "sindri/l1/p1", Sha), (pr.GetProperty("base").GetProperty("ref").GetString(), pr.GetProperty("head").GetProperty("ref").GetString(), pr.GetProperty("head").GetProperty("sha").GetString()));
    }

    [Fact]
    public async Task A_phase_whose_pull_request_is_open_updates_it_with_the_new_title_and_body_and_keeps_the_marker()
    {
        var seeded = _pulls.Seed(new OpenPull(_repo, "sindri/l1/p1", "main", Sha, "Old", "Old body", Draft: false, Marker: "sindri.pr:sindri-l1-p1"));

        var done = await Dispatch();

        Assert.Equal("updated", done.Reply?.Value);
        Assert.Empty(_pulls.Opened);
        var (updatedLink, updatedTitle, updatedBody) = Assert.Single(_pulls.Updated);
        Assert.Equal((seeded.Link, "Add login"), (updatedLink, updatedTitle));
        Assert.Equal("Evidence pack body\n\n<!-- aw-marker:sindri.pr:sindri-l1-p1 -->", updatedBody);
        Assert.Contains(seeded.Link, Content(done, "fact:pr"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_dispatch_for_the_same_head_opens_nothing_twice()
    {
        var first = await Dispatch();
        var second = await Dispatch();

        Assert.Equal("opened", first.Reply?.Value);
        Assert.Equal("updated", second.Reply?.Value);
        _ = Assert.Single(_pulls.Opened);
        _ = Assert.Single(_pulls.Pulls);
        Assert.Equal(2, _pulls.Pushed.Count);
    }

    [Fact]
    public async Task A_head_branch_with_no_pull_request_is_pushed_without_a_lease_before_the_pull_request_is_opened()
    {
        _ = await Dispatch();

        Assert.Equal(new PullBranchPush(_repo, Workspace, "sindri/l1/p1", Sha, LeaseSha: null), Assert.Single(_pulls.Pushed));
        string[] calls = ["Find", "PushBranch", "Open"];
        Assert.Equal(calls, _pulls.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_rework_pushes_the_head_branch_leased_on_the_open_pull_requests_head_and_updates_it()
    {
        const string OldSha = "fedcba9876543210fedcba9876543210fedcba98";
        var seeded = _pulls.Seed(new OpenPull(_repo, "sindri/l1/p1", "main", OldSha, "Old", "Old body", Draft: false, Marker: "sindri.pr:sindri-l1-p1"));

        var done = await Dispatch();

        Assert.Equal("updated", done.Reply?.Value);
        Assert.Equal(new PullBranchPush(_repo, Workspace, "sindri/l1/p1", Sha, LeaseSha: OldSha), Assert.Single(_pulls.Pushed));
        Assert.Equal(seeded.Link, Assert.Single(_pulls.Updated).Link);
    }

    [Theory]
    [InlineData(PullPushOutcome.HeadMoved)]
    [InlineData(PullPushOutcome.Rejected)]
    public async Task A_branch_push_that_does_not_land_replies_refused_and_opens_nothing(PullPushOutcome outcome)
    {
        _pulls.PushOutcome = outcome;

        var done = await Dispatch();

        Assert.Equal("refused", done.Reply?.Value);
        Assert.Empty(_pulls.Opened);
    }

    [Fact]
    public async Task Dispatching_twice_opens_one_pull_request()
    {
        _ = await Dispatch();

        var again = await Dispatch();

        Assert.Equal("updated", again.Reply?.Value);
        _ = Assert.Single(_pulls.Opened);
    }

    [Fact]
    public async Task The_profile_decides_draft()
    {
        _ = await _profiles.Save(new LoadedProfile(new SindriProfile(1, "dev", Workflow: new WorkflowSettings(Ship: new ShipSettings(Draft: true))), ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), Token);

        _ = await Dispatch();

        Assert.True(Assert.Single(_pulls.Opened).Draft);
    }

    [Fact]
    public async Task A_forge_that_refuses_the_call_replies_refused_without_a_pull_request()
    {
        _pulls.Failure = SindriErrors.ForgeRefused.With("host not allowed");

        var done = await Dispatch();

        Assert.Equal("refused", done.Reply?.Value);
        Assert.Null(done.Facts);
    }

    [Fact]
    public async Task A_forge_that_cannot_be_reached_fails_the_effect()
    {
        _pulls.Failure = SindriErrors.ForgeUnavailable.With("offline");

        var result = await Effect().Dispatch(Context(), Token);

        Assert.Equal(SindriErrors.ForgeUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_hand_off_without_a_pr_json_fails_the_effect_and_names_the_file()
    {
        var result = await new PrOpenEffect(_pulls, new FakeForgeRepos().Put(Workspace, _repo), new InMemoryPacketFiles(), _facts, _facts, _profiles).Dispatch(Context(), Token);

        Assert.True(result.IsError);
        Assert.Contains("pr.json", result.Error.Message, StringComparison.Ordinal);
    }

    private PrOpenEffect Effect() => new(_pulls, new FakeForgeRepos().Put(Workspace, _repo), _files, _facts, _facts, _profiles);

    private async Task<EffectDone> Dispatch()
    {
        var done = await Effect().Dispatch(Context(), Token);
        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        return done.Value;
    }

    private string Content(EffectDone done, string key) => Encoding.UTF8.GetString(_facts.Stored[done.Facts![FactKey.From(key)].Sha256]);

    private EffectContext Context()
    {
        var facts = new Dictionary<FactKey, JsonElement>
        {
            [FactKey.From("fact:handoff")] = Stored($$"""{ "hash": "sha256:aa", "dir": "{{Dir}}" }"""),
            [FactKey.From("fact:workspace")] = Stored(Workspace),
        };
        return new EffectContext(BlockId.From("effect.pr.open"), "T-4/ship/1:3:0", "C-1", FromEngine: false, "T-4/ship/1", "T-4", Attempt: 1,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts, Text: null);
    }

    private JsonElement Stored(string content)
    {
        var stored = _facts.Put(Encoding.UTF8.GetBytes(content), Token).AsTask().GetAwaiter().GetResult().Value;
        using var fact = JsonDocument.Parse($$"""{"ref":"{{stored.Ref}}","sha256":"{{stored.Sha256}}","headSha":null}""");
        return fact.RootElement.Clone();
    }
}
