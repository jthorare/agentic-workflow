using Aw.Kernel.Testing;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Tests.Evolve;

/// <summary>A held proposal gets three gate runs in all, then waits for the owner.</summary>
[Lane(TestLane.Unit)]
public sealed class ProposalStatesTests
{
    [Fact]
    public void A_clean_verdict_stages_and_counts_the_attempt() =>
        Assert.Equal(new StageOutcome(ProposalState.Staged, 1), ProposalStates.After(LeakVerdict.Clean, 0));

    [Fact]
    public void A_hold_counts_the_attempt() =>
        Assert.Equal(new StageOutcome(ProposalState.Held, 2), ProposalStates.After(LeakVerdict.HeldBy(LeakLayer.Judge), 1));

    [Theory]
    [InlineData(nameof(ProposalState.Proposed), 0, true)]
    [InlineData(nameof(ProposalState.Held), 1, true)]
    [InlineData(nameof(ProposalState.Held), 2, true)]
    [InlineData(nameof(ProposalState.Held), 3, false)]
    [InlineData(nameof(ProposalState.Staged), 1, false)]
    [InlineData(nameof(ProposalState.Rejected), 0, false)]
    [InlineData(nameof(ProposalState.Published), 1, false)]
    [InlineData(nameof(ProposalState.Merged), 1, false)]
    public void Only_proposed_or_held_with_attempts_left_may_stage(string state, int attempts, bool may) =>
        Assert.Equal(may, ProposalStates.CanStage(Enum.Parse<ProposalState>(state), attempts));
}
