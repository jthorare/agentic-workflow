using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The state words of the workflow editor (spec 4.7 and 4.8): the primary button follows the step, and each count is singular for one.</summary>
[Lane(TestLane.Unit)]
public sealed class EditorStatesTests
{
    [Fact]
    public void The_primary_button_follows_the_next_step()
    {
        Assert.Equal("Add a trigger", EditorStates.Primary(new EditorSnapshot(HasTrigger: false, HasAction: false)));
        Assert.Equal("Add an action", EditorStates.Primary(new EditorSnapshot(HasAction: false)));
        Assert.Equal("Fix 2 things", EditorStates.Primary(new EditorSnapshot(Findings: 2)));
        Assert.Equal("Test run", EditorStates.Primary(new EditorSnapshot()));
        Assert.Equal("Activate", EditorStates.Primary(new EditorSnapshot(Tested: true)));
    }

    [Fact]
    public void One_finding_says_thing_not_things() => Assert.Equal("Fix 1 thing", EditorStates.Primary(new EditorSnapshot(Findings: 1)));

    [Fact]
    public void A_running_test_shows_Testing_and_beats_the_step_words() =>
        Assert.Equal("Testing...", EditorStates.Primary(new EditorSnapshot(Testing: true, Findings: 3)));

    [Fact]
    public void A_conflict_shows_Reload_and_beats_a_running_test() =>
        Assert.Equal("Reload", EditorStates.Primary(new EditorSnapshot(Conflict: true, Testing: true, Findings: 2)));

    [Fact]
    public void Engine_down_wins_over_every_other_state_and_never_offers_Fix() =>
        Assert.Equal("Start Sindri", EditorStates.Primary(new EditorSnapshot(EngineDown: true, Conflict: true, Testing: true, Findings: 2)));

    [Theory]
    [InlineData(1, "1 thing to fix")]
    [InlineData(2, "2 things to fix")]
    [InlineData(11, "11 things to fix")]
    public void The_header_counts_findings(int count, string words) => Assert.Equal(words, EditorStates.ThingsToFix(count));

    [Theory]
    [InlineData(1, "1 thing left")]
    [InlineData(4, "4 things left")]
    public void The_checklist_counts_what_is_left(int count, string words) => Assert.Equal(words, EditorStates.ThingsLeft(count));

    [Fact]
    public void The_fixed_state_words_are_the_spec_words()
    {
        Assert.Equal("A test is running. Esc cancels it", EditorStates.TestRunningRefusal);
        Assert.Equal("Changed elsewhere. Reload", EditorStates.Conflict);
        Assert.Equal("Not tested", EditorStates.NotTested);
        Assert.Equal("Not tested yet. Activate anyway?", EditorStates.ActivateUntested);
        Assert.Equal("Test cancelled", EditorStates.TestCancelled);
        Assert.Equal("Couldn't load templates. Retry", EditorStates.TemplatesFailed);
        Assert.Equal("Not live. Last read 14:02", EditorStates.NotLive("14:02"));
    }
}
