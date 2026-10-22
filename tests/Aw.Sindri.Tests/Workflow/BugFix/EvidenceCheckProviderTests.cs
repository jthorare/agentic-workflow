using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Blocks.Evidence;

namespace Aw.Sindri.Tests.Workflow.BugFix;

/// <summary>'evidence.check' (DT spec §5.5, §8.7): the frozen check re-runs on the candidate through bugfix-state; the evidence passes when the check passes.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceCheckProviderTests
{
    private const string Candidate = """{"id":"c1","head":"commit-of-fix-1","attempt":1,"branch":"fix-1","worktree":"/work/fix-1","changedFiles":["src/app.txt"]}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BugFixRig> Recorded(bool fixes)
    {
        var rig = new BugFixRig();
        _ = fixes ? rig.State.Fixes("fix-1") : rig.State;
        _ = await rig.State.StartAttempt(rig.StateDirectory, AttemptMode.Whole, Token);
        _ = await rig.State.RecordCandidate(rig.StateDirectory, new CandidateSource("fix-1", "/work/fix-1"), Token);
        return rig;
    }

    private static EvidenceContext Context(EvidencePoint at = EvidencePoint.Head, string? address = BugFixRig.Address, string? check = BugFixRig.Check, string? candidate = Candidate)
    {
        Dictionary<FactKey, JsonElement> facts = [];
        if (check is not null)
        {
            facts[FactKey.From("fact:check")] = JsonDocument.Parse(check).RootElement.Clone();
        }

        if (candidate is not null)
        {
            facts[FactKey.From("fact:candidate")] = JsonDocument.Parse(candidate).RootElement.Clone();
        }

        return new EvidenceContext(BlockId.From("evidence.check"), "check", at, facts, address);
    }

    [Fact]
    public async Task A_check_that_passes_on_the_candidate_is_evidence_that_passes()
    {
        var rig = await Recorded(fixes: true);

        var result = await new EvidenceCheckProvider(rig.State, rig.Area).Collect(Context(), Token);

        Assert.True(result.Passed);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public async Task A_check_that_fails_on_the_candidate_does_not_pass_and_says_so()
    {
        var rig = await Recorded(fixes: false);

        var result = await new EvidenceCheckProvider(rig.State, rig.Area).Collect(Context(), Token);

        Assert.False(result.Passed);
        Assert.Contains("exit 1", Assert.Single(result.Reasons), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Base_is_not_something_this_provider_can_check()
    {
        var rig = await Recorded(fixes: true);

        var result = await new EvidenceCheckProvider(rig.State, rig.Area).Collect(Context(EvidencePoint.Base), Token);

        Assert.False(result.Passed);
        Assert.Contains("head", Assert.Single(result.Reasons), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("candidate")]
    [InlineData("address")]
    [InlineData("frozen")]
    public async Task Evidence_that_cannot_be_collected_does_not_pass_and_names_what_is_missing(string missing)
    {
        var rig = await Recorded(fixes: true);
        var context = missing switch
        {
            "check" => Context(check: null),
            "candidate" => Context(candidate: null),
            "address" => Context(address: null),
            _ => Context(check: """{"kind":"manual","file":"t","command":["x"]}"""),
        };

        var result = await new EvidenceCheckProvider(rig.State, rig.Area).Collect(context, Token);

        Assert.False(result.Passed);
        Assert.NotEmpty(Assert.Single(result.Reasons));
        Assert.DoesNotContain(rig.State.Calls, static c => c.StartsWith("recheck", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_refusal_from_bugfix_state_does_not_pass_and_carries_its_reason()
    {
        var rig = new BugFixRig();   // no attempt started: the candidate is unknown to bugfix-state

        var result = await new EvidenceCheckProvider(rig.State, rig.Area).Collect(Context(), Token);

        Assert.False(result.Passed);
        Assert.Contains("bugfix-state", Assert.Single(result.Reasons), StringComparison.Ordinal);
    }
}
