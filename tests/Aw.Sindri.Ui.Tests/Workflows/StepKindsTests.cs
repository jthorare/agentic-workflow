using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>One name per step kind (#3074, W11) and the step's accessible name with its place (#3073, W12).</summary>
[Lane(TestLane.Unit)]
public sealed class StepKindsTests
{
    [Fact]
    public void If_is_called_If_not_condition__and_Send_notification_not_notify()
    {
        Assert.Equal("If", StepKinds.Name("condition"));
        Assert.Equal("Send notification", StepKinds.Name("notify"));
    }

    [Fact]
    public void The_catalog_title_wins_over_the_known_name() => Assert.Equal("Branch", StepKinds.Name("condition", "Branch"));

    [Fact]
    public void An_unknown_type_reads_as_words_never_as_an_id()
    {
        Assert.Equal("My block", StepKinds.Name("my_block"));
        Assert.Equal("Plain", StepKinds.Name("plain", "  "));
    }

    [Fact]
    public void Name_audit__no_known_kind_shows_its_type_id_and_every_kind_has_a_title()
    {
        Assert.True(StepKinds.Fallback.Count >= 20);
        foreach (var (type, title) in StepKinds.Fallback)
        {
            Assert.NotEmpty(title);
            Assert.DoesNotContain('_', title);
            Assert.Equal(title, StepKinds.Name(type));
            Assert.Equal(title, new GraphNodeViewModel("x", "X", type, x: 0, y: 0, status: null).Kind);
        }
    }

    [Fact]
    public void The_canvas_kind_for_a_condition_step_is_If() => Assert.Equal("If", new GraphNodeViewModel("flagged", "Flagged?", "condition", x: 0, y: 0, status: null).Kind);

    [Fact]
    public void A_step_is_named_with_its_title_kind_and_place() =>
        Assert.Equal("Check, Prompt step, 2 of 4", new GraphNodeViewModel("check", "Check", "prompt", x: 0, y: 0, status: null).PositionName(position: 2, count: 4));

    [Fact]
    public void A_trigger_step_is_named_the_same_way() =>
        Assert.Equal("Every weekday at 7:00, Schedule step, 1 of 4", new GraphNodeViewModel("weekdays", "Every weekday at 7:00", "schedule", x: 0, y: 0, status: null).PositionName(position: 1, count: 4));
}
