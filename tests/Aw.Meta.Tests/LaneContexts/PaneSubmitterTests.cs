using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Tests.LaneContexts;

public sealed class PaneSubmitterTests
{
    private const string Rule = "────────────";

    [Fact, Trait("Lane", "unit")]
    public void Text_left_in_the_input_box_means_the_enter_was_swallowed()
    {
        var capture = $"output\n{Rule}\n❯ Read and continue from /home/x/digests/t1-api-2026-10-17.md\n{Rule}\n";

        Assert.True(PaneSubmitter.InputHolds(capture, "Read and continue from /home/x/digests/t1-api-2026-10-17.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_input_box_or_text_above_the_rule_means_it_was_submitted()
    {
        var capture = $"❯ Read and continue from /home/x/digests/t1-api-2026-10-17.md\n● working\n{Rule}\n❯ \n{Rule}\n";

        Assert.False(PaneSubmitter.InputHolds(capture, "Read and continue from /home/x/digests/t1-api-2026-10-17.md"));
        Assert.False(PaneSubmitter.InputHolds("no prompt at all", "Read and continue"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wrapped_line_in_the_input_box_is_still_found()
    {
        var capture = $"{Rule}\n❯ Read and continue from /home/x/di\n  gests/t1-api-2026-10-17.md\n{Rule}\n";

        Assert.True(PaneSubmitter.InputHolds(capture, "Read and continue from /home/x/digests/t1-api-2026-10-17.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Submit_types_waits_presses_Enter_waits_and_reads_the_pane_back()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([("t1-api", "/clear")], world.Panes.Woken);
        Assert.Equal(1, world.Panes.Enters);
        Assert.Equal([PaneSubmitter.Pause, PaneSubmitter.Pause], world.Delay.Waits);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_Enter_that_never_gets_through_is_pressed_four_times_in_all_and_then_fails_with_nothing_submitted()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");
        world.Panes.SwallowEnters = 99;

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "Read and continue from /d.md", CancellationToken.None);

        Assert.Equal(Core.MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Equal(PaneSubmitter.Retries + 1, world.Panes.Enters);
        Assert.Empty(world.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_exact_line_that_lands_without_its_first_keys_presses_no_Enter_and_names_what_the_box_shows()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");
        world.Panes.EatNextTyped = 3;

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None, exact: true);

        Assert.Equal(Core.MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Contains("'ear'", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("still in the input box", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, world.Panes.Enters);
        Assert.Empty(world.Panes.Woken);
    }

    // #3521: the junk must not sit in the box, or the next send (a continue line, a usage nudge) appends to it and submits both.
    [Fact, Trait("Lane", "unit")]
    public async Task A_mismatched_exact_line_is_cleared_from_the_box_and_the_error_says_so()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");
        world.Panes.EatNextTyped = 3;

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None, exact: true);

        Assert.True(result.IsError);
        Assert.Contains("cleared the input box", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, world.Panes.InputBox("t1-api"));
        Assert.Equal(0, world.Panes.Enters);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_retry_after_a_cleared_mismatch_submits_the_line_alone()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");
        world.Panes.EatNextTyped = 3;
        _ = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None, exact: true);

        var again = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None, exact: true);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.Message : string.Empty);
        Assert.Equal([("t1-api", "/clear")], world.Panes.Woken);
        Assert.Equal(1, world.Panes.Enters);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_box_the_clear_does_not_empty_is_named_as_still_holding_the_junk()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");
        world.Panes.EatNextTyped = 3;
        world.Panes.IgnoreClear = true;

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None, exact: true);

        Assert.True(result.IsError);
        Assert.Contains("could not clear", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, world.Panes.Enters);
    }

    // #3521: with no prompt line to read, the exact check can't run; pressing Enter anyway is how "ear" got submitted in #3443.
    [Fact, Trait("Lane", "unit")]
    public async Task An_exact_line_with_no_readable_input_box_presses_no_Enter_clears_the_typing_and_names_the_last_lines()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");
        world.Panes.Show("t1-api", "● Working on the build\n  esc to interrupt\n");

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None, exact: true);

        Assert.Equal(Core.MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Contains("esc to interrupt", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("no Enter was pressed", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, world.Panes.Enters);
        Assert.Empty(world.Panes.Woken);
        Assert.Equal(1, world.Panes.Clears);
        Assert.Equal(string.Empty, world.Panes.InputBox("t1-api"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_without_the_exact_check_still_goes_in_when_no_input_box_is_readable()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");
        world.Panes.Show("t1-api", "codex output\n› \n");

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "Read and continue from /d.md", CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(1, world.Panes.Enters);
        Assert.Equal(0, world.Panes.Clears);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_exact_line_that_lands_whole_is_submitted_once()
    {
        var world = new Testing.MetaWorld();
        world.Panes.Put("t1-api", "claude");

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-api", "/clear", CancellationToken.None, exact: true);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal([("t1-api", "/clear")], world.Panes.Woken);
        Assert.Equal(1, world.Panes.Enters);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_type_presses_no_key()
    {
        var world = new Testing.MetaWorld();

        var result = await new PaneSubmitter(world.Panes, world.Delay).Submit("t1-gone", "/clear", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, world.Panes.Enters);
    }
}
