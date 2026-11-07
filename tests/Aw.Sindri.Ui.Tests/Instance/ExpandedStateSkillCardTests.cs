using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The expanded state holds the skill card of a `step.skill.*` state (skill flows spec §5.2): shown, removed, and kept across a re-read of the same words.</summary>
[Lane(TestLane.Unit)]
public sealed class ExpandedStateSkillCardTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private ExpandedStateViewModel Expanded()
    {
        var detail = FixtureInstances.Detail("incident-loop");
        return new ExpandedStateViewModel(detail.Current, detail.Address, _clipboard, _clock);
    }

    private SkillCardInput Input(string? question = null)
    {
        var block = new SkillBlockView("office-hours", "officeHours", "step.skill.office-hours", 1, "Shapes an idea.", [], [], ["done"], Pushes: false, "sha", "<skills>/officeHours/block.yaml");
        var cell = new StateCell("mitigate", Order: 1, BranchOf: null, StateGlyph.Active, Badge: null, Round: null, [], Desk: false, Child: false, Children: null, Outcome: null, Terminal: false, Block: "step.skill.office-hours");
        var run = new StepRun("mitigate.1", "mitigate", 1, MaxAttempts: null, "running", "agent", A.Noon, EndedAt: null, DurationMs: null, Outcome: null, Tokens: null, Actor: null);
        var row = question is null ? null : new NeedsYouRowView("skill:T-1:mitigate", "skillWaiting", "skill", Subject: null, Item: null, "Mitigate", "waiting", Repo: null, "calm", A.Noon, RecordedOnly: false, Notice: null, AnswerKind: null, PrimaryAction: null, [], "cli",
            OwnerWaiting: new OwnerWaitingView("LCH-1", "question", question, "/r.json", Detail: null, "T-1", "mitigate", Attempt: 1));
        return new SkillCardInput(new SkillsView([block]), "T-1", "Mitigate", cell, run, row is null ? [] : [row], [], Cost: null, new FakeLauncher(), _clipboard, _clock);
    }

    [Fact]
    public void A_state_with_no_skill_block_has_no_card()
    {
        var expanded = Expanded();

        Assert.Equal((null, false), (expanded.SkillCard, expanded.HasSkillCard));
    }

    [Fact]
    public void Showing_a_card_makes_it_the_states_and_null_removes_it()
    {
        var expanded = Expanded();

        expanded.ShowSkillCard(Input());
        var shown = (expanded.HasSkillCard, expanded.SkillCard?.Sentence);
        expanded.ShowSkillCard(input: null);

        Assert.Equal((true, "Shapes an idea."), shown);
        Assert.Equal((null, false), (expanded.SkillCard, expanded.HasSkillCard));
    }

    [Fact]
    public void A_re_read_with_the_same_question_keeps_the_card_and_does_not_announce_it_again()
    {
        var expanded = Expanded();
        expanded.ShowSkillCard(Input("Which repo?"));
        var first = expanded.SkillCard;
        first!.Announce();
        first.Voice.Quiet();

        expanded.ShowSkillCard(Input("Which repo?"));

        expanded.SkillCard!.Announce();
        Assert.Same(first, expanded.SkillCard);
        Assert.False(expanded.SkillCard.Voice.IsAnnouncing);   // announced once: the second Announce, after the re-read, says nothing
    }

    [Fact]
    public void A_new_question_is_a_new_card_announced_on_its_own()
    {
        var expanded = Expanded();
        expanded.ShowSkillCard(Input("Which repo?"));

        expanded.ShowSkillCard(Input("Which branch?"));
        expanded.SkillCard!.Announce();

        Assert.Equal("Mitigate is waiting on you: Which branch?", expanded.SkillCard!.Voice.Announcement);
    }

    [Fact]
    public void A_new_expanded_state_carries_the_announced_question_so_the_same_question_is_not_said_again()
    {
        var before = Expanded();
        before.ShowSkillCard(Input("Which repo?"));
        before.SkillCard!.Announce();
        var after = Expanded();

        after.CarrySkillCard(before);
        after.ShowSkillCard(Input("Which repo?"));
        after.SkillCard!.Voice.Quiet();
        after.SkillCard.Announce();

        Assert.False(after.SkillCard.Voice.IsAnnouncing);
    }
}
