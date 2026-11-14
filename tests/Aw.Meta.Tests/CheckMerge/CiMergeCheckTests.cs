using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.CheckMerge;

public sealed class CiMergeCheckTests
{
    private readonly MetaWorld _world = new();

    private CheckMergeService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles, _world.Messages));

    private async Task CiTeam()
    {
        await _world.Ready();
        var team = (await _world.Config.Load(CancellationToken.None)).Value;
        _ = await _world.Config.Save(team with { Gate = GateConfig.Empty with { Mode = "ci" }, MergeOrder = [new MergeOrderRule(189, 190)] }, CancellationToken.None);
    }

    private async Task Save(string head, GateVerdict verdict, params string[] scopes)
    {
        GateStep[] steps = [new("build", verdict, "SUCCESS"), new("review", GateVerdict.Pass, "APPROVED")];
        _ = await _world.Gates.Save(new GateRecord(190, head, "oldmain", scopes, steps, verdict, A.At(5)), CancellationToken.None);
    }

    private void Script(string head = "headsha", string earlier = "MERGED", string merge = "CLEAN", string review = "APPROVED", bool liveFails = false) =>
        _ = _world.Runner
            .When("gh pr view 190 --json headRefOid,baseRefName,reviewDecision,mergeStateStatus", liveFails ? string.Empty : $$"""{"headRefOid":"{{head}}","baseRefName":"main","reviewDecision":"{{review}}","mergeStateStatus":"{{merge}}"}""", liveFails ? 1 : 0)
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", $$"""{"headRefOid":"{{head}}","isDraft":false,"title":"Add x","state":"OPEN"}""")
            .When("gh pr view 189 --json state", $$"""{"state":"{{earlier}}"}""")
            .When("rev-parse --verify --quiet origin/main", "newmain\n")
            .When("git fetch");

    private async Task<CheckMergeResponse> Check()
    {
        var result = await Service().Handle(new CheckMergeRequest(190), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string[] Failed(CheckMergeResponse response) => [.. response.Checks.Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_passing_ci_record_at_the_current_head_is_ready_whatever_main_did_since()
    {
        await CiTeam();
        await Save("headsha", GateVerdict.Pass, "ci");
        Script();

        var response = await Check();

        Assert.True(response.Ready, string.Join("; ", response.Checks.Where(c => !c.Ok).Select(c => c.Name + ": " + c.Detail)));
        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains("merge-tree", StringComparison.Ordinal) || l.Contains("diff --name-only", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_passing_ci_record_at_an_older_head_fails_as_a_moved_head()
    {
        await CiTeam();
        await Save("oldhead", GateVerdict.Pass, "ci");
        Script(head: "newhead");

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["head"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_moved_after_the_gate_refuses_on_head_even_when_github_is_clean_and_approved()
    {
        await CiTeam();
        await Save("headsha", GateVerdict.Pass, "ci");
        Script(head: "head2");

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["head"], Failed(response));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("DIRTY", "APPROVED", "merge-state")]
    [InlineData("CLEAN", "REVIEW_REQUIRED", "review-decision")]
    public async Task A_gate_that_passed_is_refused_when_github_is_no_longer_clean_and_approved_at_merge_time(string merge, string review, string failed)
    {
        await CiTeam();
        await Save("headsha", GateVerdict.Pass, "ci");
        Script(merge: merge, review: review);

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal([failed], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_live_read_of_the_merge_state_refuses()
    {
        await CiTeam();
        await Save("headsha", GateVerdict.Pass, "ci");
        Script(liveFails: true);

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["merge-state", "review-decision"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_ci_record_fails_the_gate_result()
    {
        await CiTeam();
        await Save("headsha", GateVerdict.Fail, "ci");
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["gate-result"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_local_record_in_a_ci_team_is_not_a_ci_gate()
    {
        await CiTeam();
        await Save("headsha", GateVerdict.Pass, "repo", "dotnet");
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Contains("gate", Failed(response), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merge_order_and_draft_still_hold_in_ci_mode()
    {
        await CiTeam();
        await Save("headsha", GateVerdict.Pass, "ci");
        Script(earlier: "OPEN");

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["order"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_local_team_still_needs_the_scopes_and_main_checks()
    {
        await _world.ReadyWithGate();
        await Save("headsha", GateVerdict.Pass, "repo", "dotnet");
        _ = _world.Runner
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", """{"headRefOid":"headsha","isDraft":false,"title":"Add x","state":"OPEN"}""")
            .When("rev-parse --verify --quiet origin/main", "newmain\n")
            .When("diff --name-only oldmain...headsha", "src/a.cs\n")
            .When("merge-tree", exitCode: 1)
            .When("git fetch")
            .When("gh api graphql", MergeRun.QueueAnswers.Off);

        var response = await Check();

        Assert.Contains("main", Failed(response), StringComparer.Ordinal);
    }
}
