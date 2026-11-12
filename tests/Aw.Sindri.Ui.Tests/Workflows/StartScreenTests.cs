using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The start screen (spec 4.1): templates A, B and C and Blank; no "Describe it" before P6; arrows move, Enter chooses.</summary>
[Lane(TestLane.Unit)]
public sealed class StartScreenTests
{
    [Fact]
    public void The_screen_offers_the_three_templates_then_blank_and_nothing_else()
    {
        var screen = new StartScreenViewModel();

        Assert.Equal(["Review every PR", "Plan a labelled issue", "Watch main, open an issue on red", "Blank"], screen.Entries.Select(e => e.Title), StringComparer.Ordinal);
        Assert.Equal(["A", "B", "C", string.Empty], screen.Entries.Select(e => e.Letter), StringComparer.Ordinal);
        Assert.DoesNotContain(screen.Entries, e => e.Title.Contains("Describe", StringComparison.OrdinalIgnoreCase));
        Assert.Equal([false, false, false, true], screen.Entries.Select(e => e.IsBlank));
    }

    [Fact]
    public void Each_card_shows_trigger_steps_needs_and_last_ran()
    {
        var cards = new StartScreenViewModel().Entries.Take(3).ToList();

        Assert.Equal(["PR opened", "Issue labelled", "Schedule, weekdays 7:00"], cards.Select(c => c.Trigger), StringComparer.Ordinal);
        Assert.Equal(["3 · trigger, agent, notification", "3 · trigger, prompt, comment", "4 · schedule, check, if, issue"], cards.Select(c => c.Steps), StringComparer.Ordinal);
        Assert.Equal(["GitHub connection", "GitHub connection", "nothing"], cards.Select(c => c.Needs), StringComparer.Ordinal);
        Assert.All(cards, c => Assert.Equal("never on this box", c.LastRan));
        Assert.All(cards, c => Assert.NotEmpty(c.Name));
    }

    [Fact]
    public void Card_A_does_not_list_an_agent_among_its_needs() =>
        Assert.DoesNotContain("agent", new StartScreenViewModel().Entries[0].Needs, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Blank_names_an_empty_trigger_slot_and_has_no_template()
    {
        var blank = new StartScreenViewModel().Entries[3];

        Assert.Equal(string.Empty, blank.Name);
        Assert.Contains("empty trigger slot", blank.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_A_is_selected_first()
    {
        var screen = new StartScreenViewModel();

        Assert.Equal(0, screen.SelectedIndex);
        Assert.Equal("Review every PR", screen.Selected.Title);
    }

    [Fact]
    public void Right_and_left_move_one_entry_and_stop_at_the_edges()
    {
        var screen = new StartScreenViewModel();

        screen.Move(StartMove.Left);
        Assert.Equal(0, screen.SelectedIndex);
        screen.Move(StartMove.Right);
        screen.Move(StartMove.Right);
        Assert.Equal(2, screen.SelectedIndex);
        screen.Move(StartMove.Right);
        Assert.Equal(3, screen.SelectedIndex);
        screen.Move(StartMove.Right);
        Assert.Equal(3, screen.SelectedIndex);
    }

    [Fact]
    public void Down_from_a_template_goes_to_blank_and_up_from_blank_goes_to_A()
    {
        var screen = new StartScreenViewModel();
        screen.Select(1);

        screen.Move(StartMove.Down);
        Assert.True(screen.Selected.IsBlank);
        screen.Move(StartMove.Up);
        Assert.Equal(0, screen.SelectedIndex);
    }

    [Fact]
    public void Selecting_out_of_range_changes_nothing()
    {
        var screen = new StartScreenViewModel();
        screen.Select(2);

        screen.Select(9);
        screen.Select(-1);

        Assert.Equal(2, screen.SelectedIndex);
    }

    [Fact]
    public void Enter_chooses_the_selected_entry()
    {
        var screen = new StartScreenViewModel();
        screen.Move(StartMove.Right);

        Assert.Equal("Plan a labelled issue", screen.Choose().Title);
    }

    [Fact]
    public void Moving_tells_the_view_the_selection_changed()
    {
        var screen = new StartScreenViewModel();
        var raised = new List<string?>();
        screen.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        screen.Move(StartMove.Right);

        Assert.Contains(nameof(StartScreenViewModel.SelectedIndex), raised, StringComparer.Ordinal);
        Assert.Contains(nameof(StartScreenViewModel.Selected), raised, StringComparer.Ordinal);
    }
}
