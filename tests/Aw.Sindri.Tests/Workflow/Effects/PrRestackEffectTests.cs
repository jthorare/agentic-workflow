using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.restack' (PB spec §4.6): rebases the workspace onto the new base, pushes with a lease on the old head, then retargets the pull request.
/// 'restacked', 'nothing', 'conflict' (no push) or 'refused' (the lease failed or the remote refused).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrRestackEffectTests
{
    private const string Link = "https://forge.example/acme/web/pull/1";
    private const string Workspace = "/work/tree";

    private static readonly PullRepo _repo = new("forge.example", "acme", "web");

    private readonly FakePullRequests _pulls = new(["forge.example"]);
    private readonly FakeWorkspaceRebase _rebase = new();
    private readonly InMemoryFactStore _facts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_moved_branch_is_pushed_with_a_lease_on_the_old_head_and_the_pull_request_retargeted()
    {
        await Seed();
        _rebase.Script(Workspace, new RebaseOutcome(RebaseStatus.Rebased, "new1", []));

        var done = await Dispatch(onto: "main");

        Assert.Equal("restacked", done.Reply?.Value);
        Assert.Equal([(Workspace, "main", "b0")], _rebase.Calls);
        Assert.Equal(["PushBranch", "PushBranch", "Retarget"], _pulls.Calls);   // the first seeds the branch's tip on the fake
        Assert.Equal("main", _pulls.Pulls.Single().Base);
    }

    [Fact]
    public async Task A_conflict_replies_conflict_and_pushes_nothing()
    {
        await Seed();
        _rebase.Script(Workspace, new RebaseOutcome(RebaseStatus.Conflict, NewHead: null, ["a.txt"]));

        var done = await Dispatch(onto: "main");

        Assert.Equal("conflict", done.Reply?.Value);
        Assert.Equal(["PushBranch"], _pulls.Calls);
    }

    [Fact]
    public async Task A_branch_already_on_the_base_with_the_base_already_set_replies_nothing()
    {
        await Seed();
        _rebase.Script(Workspace, new RebaseOutcome(RebaseStatus.Nothing, NewHead: null, []));

        var done = await Dispatch(onto: "stack-parent");

        Assert.Equal("nothing", done.Reply?.Value);
        Assert.Equal(["PushBranch"], _pulls.Calls);
    }

    [Fact]
    public async Task A_branch_already_on_the_new_base_is_still_retargeted()
    {
        await Seed();
        _rebase.Script(Workspace, new RebaseOutcome(RebaseStatus.Nothing, NewHead: null, []));

        var done = await Dispatch(onto: "main");

        Assert.Equal("restacked", done.Reply?.Value);
        Assert.Equal(["PushBranch", "Retarget"], _pulls.Calls);
    }

    [Fact]
    public async Task A_moved_head_refuses_and_retargets_nothing()
    {
        await Seed(tip: "somebody-elses");
        _rebase.Script(Workspace, new RebaseOutcome(RebaseStatus.Rebased, "new1", []));

        var done = await Dispatch(onto: "main");

        Assert.Equal("refused", done.Reply?.Value);
        Assert.DoesNotContain("Retarget", _pulls.Calls);
    }

    [Fact]
    public async Task A_missing_snapshot_is_an_error()
    {
        var context = new EffectContext(BlockId.From("effect.pr.restack"), "T-4/pr/1:3:0", "C-1", FromEngine: false, "T-4/pr/1", "T-4", 1,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Text: null);

        var done = await Effect().Dispatch(context, Token);

        Assert.True(done.IsError);
        Assert.Empty(_rebase.Calls);
    }

    private PrRestackEffect Effect() => new(_pulls, _rebase, _facts);

    /// <summary>The pull request, its branch 'feature' at 'old1' on the fake forge (the tip is seeded by a first push; <paramref name="tip"/> differs to move it).</summary>
    private async Task Seed(string tip = "old1")
    {
        _ = _pulls.Add(_repo, "feature", "old1");
        _ = await _pulls.PushBranch(new PullBranchPush(_repo, Workspace, "feature", tip, LeaseSha: null), Token);
    }

    private async Task<EffectDone> Dispatch(string onto)
    {
        var state = $$"""{"url":"{{Link}}","base":{"ref":"stack-parent","sha":"b0","onto":"{{onto}}"},"head":{"ref":"feature","sha":"old1"}}""";
        var facts = new Dictionary<FactKey, JsonElement>
        {
            [FactKey.From("fact:prState")] = await Stored(state),
            [FactKey.From("fact:workspace")] = await Stored($$"""{"path":"{{Workspace}}"}"""),
        };
        var context = new EffectContext(BlockId.From("effect.pr.restack"), "T-4/pr/1:3:0", "C-1", FromEngine: false, "T-4/pr/1", "T-4", 1,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts, Text: null);

        var done = await Effect().Dispatch(context, Token);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        return done.Value;
    }

    private async Task<JsonElement> Stored(string json)
    {
        var stored = (await _facts.Put(Encoding.UTF8.GetBytes(json), Token)).Value;
        using var fact = JsonDocument.Parse($$"""{"ref":"{{stored.Ref}}","sha256":"{{stored.Sha256}}","headSha":null}""");
        return fact.RootElement.Clone();
    }
}
