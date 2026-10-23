using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>Who a list row says the instance waits on.</summary>
[Lane(TestLane.Unit)]
public sealed class WaitingOnTests
{
    [Theory]
    [InlineData(StateChip.Decision)]
    [InlineData(StateChip.Parked)]
    [InlineData(StateChip.Escalated)]
    [InlineData(StateChip.WithYou)]
    public void A_chip_that_needs_a_person_waits_on_you(string word) =>
        Assert.Equal("you", WaitingOn.Of(new StateChip(word, ChipTone.Attn), deskOpen: false, agentRunOpen: false, ended: false));

    [Fact]
    public void An_open_desk_run_waits_on_you_even_under_another_chip() =>
        Assert.Equal("you", WaitingOn.Of(new StateChip(StateChip.Running, ChipTone.Neutral), deskOpen: true, agentRunOpen: false, ended: false));

    [Fact]
    public void An_open_agent_run_waits_on_the_agent() =>
        Assert.Equal("agent", WaitingOn.Of(new StateChip(StateChip.Running, ChipTone.Neutral), deskOpen: false, agentRunOpen: true, ended: false));

    [Theory]
    [InlineData(StateChip.Running)]
    [InlineData(StateChip.Queued)]
    public void Anything_else_while_it_lives_waits_on_the_engine(string word) =>
        Assert.Equal("engine", WaitingOn.Of(new StateChip(word, ChipTone.Neutral), deskOpen: false, agentRunOpen: false, ended: false));

    [Fact]
    public void An_ended_instance_waits_on_nobody() =>
        Assert.Null(WaitingOn.Of(new StateChip(StateChip.Done, ChipTone.Neutral), deskOpen: false, agentRunOpen: false, ended: true));
}
