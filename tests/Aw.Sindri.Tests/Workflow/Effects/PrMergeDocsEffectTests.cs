using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.merge-docs' (PB spec §4.6, BD9, W23): the docs pull request is retargeted onto the default branch, then merged with the head the forge shows now as the
/// expected head. 'merged' when it merges, 'none' when there is nothing open to merge, 'pending' for everything the owner has to look at (checks not green, a conflict,
/// a head that moved, a merge the forge blocked).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrMergeDocsEffectTests
{
    private const string Link = "https://forge.example/acme/web/pull/9";
    private const string Head = "d0c5d0c5";

    private readonly RecordingPulls _pulls = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PullView Docs(PullState state = PullState.Open, bool? mergeable = true, PullChecksState checks = PullChecksState.Passing) =>
        new(Link, state, Head, mergeable, ReviewDecision: null, UnresolvedThreads: 0, new PullChecks(checks, Head, []));

    private static JsonElement Literal(string text) => JsonDocument.Parse(JsonSerializer.Serialize(text)).RootElement.Clone();

    private async Task<Result<EffectDone>> Run(string pr = Link, string? method = null)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["pr"] = await Stored(pr) };
        if (method is not null)
        {
            arguments["method"] = Literal(method);
        }

        var context = new EffectContext(BlockId.From("effect.pr.merge-docs"), "T-4/pr/1:3:0", "cmd-1", FromEngine: false, "T-4/pr/1", "T-4", Attempt: 1, arguments, new Dictionary<FactKey, JsonElement>(), Text: null);
        return await new PrMergeDocsEffect(_pulls, _facts, _profiles).Dispatch(context, Token);
    }

    private async Task<JsonElement> Stored(string content)
    {
        var put = await _facts.Put(Encoding.UTF8.GetBytes(content), Token);
        return JsonDocument.Parse($$"""{"ref":"{{put.Value.Ref}}","sha256":"{{put.Value.Sha256}}"}""").RootElement.Clone();
    }

    [Fact]
    public async Task The_docs_pull_request_is_retargeted_onto_the_default_branch_then_merged_on_its_current_head()
    {
        _pulls.GetAnswer = Docs();
        _pulls.DefaultBranchAnswer = "trunk";

        var done = await Run();

        Assert.Equal(new EffectDone(OutcomeName.From("merged"), Existed: false), done.Value);
        Assert.Equal((Link, "trunk"), Assert.Single(_pulls.Retargets));
        Assert.Equal(new PullMerge(Link, PullMergeMethod.Squash, Head), Assert.Single(_pulls.Merges));
        Assert.True(_pulls.Calls.IndexOf("Retarget") < _pulls.Calls.IndexOf("Merge"));
    }

    [Fact]
    public async Task The_pr_argument_may_be_the_pull_request_object()
    {
        _pulls.GetAnswer = Docs();

        var done = await Run($$"""{"link":"{{Link}}","head":{"ref":"docs/x","sha":"{{Head}}"}}""");

        Assert.Equal("merged", done.Value.Reply?.Value);
    }

    [Fact]
    public async Task The_method_the_recipe_names_is_used()
    {
        _pulls.GetAnswer = Docs();

        _ = await Run(method: "rebase");

        Assert.Equal(PullMergeMethod.Rebase, Assert.Single(_pulls.Merges).Method);
    }

    [Theory]
    [InlineData(PullState.Merged)]
    [InlineData(PullState.Closed)]
    public async Task A_docs_pull_request_that_is_not_open_has_nothing_to_merge(PullState state)
    {
        _pulls.GetAnswer = Docs(state);

        var done = await Run();

        Assert.Equal("none", done.Value.Reply?.Value);
        Assert.Empty(_pulls.Merges);
        Assert.Empty(_pulls.Retargets);
    }

    [Theory]
    [InlineData(PullChecksState.Pending)]
    [InlineData(PullChecksState.Failing)]
    public async Task Checks_that_are_not_green_leave_it_pending_unmerged(PullChecksState checks)
    {
        _pulls.GetAnswer = Docs(checks: checks);

        var done = await Run();

        Assert.Equal("pending", done.Value.Reply?.Value);
        Assert.Empty(_pulls.Merges);
    }

    [Fact]
    public async Task A_conflict_leaves_it_pending_unmerged()
    {
        _pulls.GetAnswer = Docs(mergeable: false);

        Assert.Equal("pending", (await Run()).Value.Reply?.Value);
        Assert.Empty(_pulls.Merges);
    }

    [Fact]
    public async Task A_head_that_differs_from_the_one_the_argument_names_is_pending_and_never_merged()
    {
        _pulls.GetAnswer = Docs();

        var done = await Run("""{"link":"https://forge.example/acme/web/pull/9","head":{"sha":"0ther"}}""");

        Assert.Equal("pending", done.Value.Reply?.Value);
        Assert.Empty(_pulls.Merges);
    }

    [Theory]
    [InlineData(PullMergeStatus.Blocked)]
    [InlineData(PullMergeStatus.HeadMoved)]
    public async Task A_merge_the_forge_refuses_is_pending_and_never_forced(PullMergeStatus status)
    {
        _pulls.GetAnswer = Docs();
        _pulls.MergeAnswer = new PullMergeOutcome(status, MergedSha: null, "why");

        Assert.Equal("pending", (await Run()).Value.Reply?.Value);
        Assert.Single(_pulls.Merges);
    }

    [Fact]
    public async Task A_missing_link_is_refused_before_the_forge_is_touched()
    {
        var done = await Run("{}");

        Assert.True(done.IsError);
        Assert.Empty(_pulls.Calls);
    }

    [Fact]
    public async Task A_forge_that_cannot_retarget_fails_the_effect_and_merges_nothing()
    {
        _pulls.GetAnswer = Docs();
        _pulls.RetargetAnswer = SindriErrors.ForgeUnavailable.With("down");

        var done = await Run();

        Assert.True(done.IsError);
        Assert.Empty(_pulls.Merges);
    }
}
