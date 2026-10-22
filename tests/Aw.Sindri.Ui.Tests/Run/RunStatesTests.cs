using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>The one map from a chip word to the run canvas's state, word and brushes (F3 spec 3 §3).</summary>
[Lane(TestLane.Unit)]
public sealed class RunStatesTests
{
    [Theory]
    [InlineData("RUNNING", "Running", "Running", "Brush.PrimaryHover", "Brush.PrimarySoft")]
    [InlineData("DONE", "Done", "Done", "Brush.Success", "Brush.SuccessSoft")]
    [InlineData("FAILED", "Failed", "Failed", "Brush.Error", "Brush.ErrorSoft")]
    [InlineData("DIDN'T WORK OUT", "DidntWorkOut", "Didn't work out", "Brush.Attention", "Brush.AttentionSoft")]
    [InlineData("CANCELLED", "Cancelled", "Cancelled", "Brush.InkMuted", "Brush.Surface2")]
    [InlineData("DECISION", "Waiting", "Waiting for you", "Brush.Attention", "Brush.AttentionSoft")]
    [InlineData("PARKED", "Paused", "Paused, needs you", "Brush.Attention", "Brush.AttentionSoft")]
    [InlineData("ESCALATED", "Paused", "Paused, needs you", "Brush.Attention", "Brush.AttentionSoft")]
    [InlineData("WITH YOU", "TookOver", "You took over", "Brush.Violet", "Brush.VioletSoft")]
    [InlineData("QUEUED", "NotStarted", "Not started", "Brush.InkSubtle", "Brush.Surface2")]
    public void Every_chip_word_maps_to_one_state_style(string word, string state, string text, string brush, string soft)
    {
        var mapped = RunStates.FromChip(RunFixtures.Chip(word));

        Assert.Equal(Enum.Parse<RunNodeState>(state), mapped);
        Assert.Equal(text, RunStates.Word(mapped));
        Assert.Equal((brush, soft), (RunStates.Brushes(mapped).Text, RunStates.Brushes(mapped).Soft));
    }

    [Fact]
    public void Didnt_work_out_takes_the_served_attention_tone_not_a_neutral_one()
    {
        var chip = RunFixtures.Chip("DIDN'T WORK OUT");

        Assert.Equal(Aw.Sindri.Ui.Api.Workflow.ChipTone.Attn, chip.Tone);
        Assert.Equal("Brush.Attention", RunStates.Brushes(RunStates.FromChip(chip)).Text);
    }

    [Fact]
    public void A_word_this_build_does_not_know_reads_as_not_started() =>
        Assert.Equal(RunNodeState.NotStarted, RunStates.FromChip(new Aw.Sindri.Ui.Api.Workflow.StateChip("SOMETHING NEW", Aw.Sindri.Ui.Api.Workflow.ChipTone.Neutral)));

    [Fact]
    public void Every_state_has_a_word_and_a_distinct_glyph_word_pair()
    {
        var words = Enum.GetValues<RunNodeState>().Select(RunStates.Word).ToList();

        Assert.Equal(words.Count, words.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_edge_pills_are_plain_and_an_unknown_outcome_shows_as_served()
    {
        string[] outcomes = ["scoped", "designed", "planned", "resolved", "unresolved", "redesign", "cancelled", "brand-new"];

        Assert.Equal(
            ["scope approved", "design approved", "plan approved", "merged", "could not finish", "needs a redesign", "stopped", "brand-new"],
            outcomes.Select(Labels.EdgeLabel),
            StringComparer.Ordinal);
        Assert.Equal("again, up to 4 times", Labels.RunLoopPill(4));
    }
}
