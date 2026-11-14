using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents;

/// <summary>The chip of a one-off in the starting state (agents spec §15.4).</summary>
[Lane(TestLane.Unit)]
public sealed class SessionChipsStartingTests
{
    [Theory]
    [InlineData(0, true, "Starting")]
    [InlineData(3999, false, "Starting")]
    [InlineData(4000, true, "Answer in the pane")]
    [InlineData(60000, true, "Answer in the pane")]
    [InlineData(4000, false, "No recent activity")]
    public void The_chip_changes_after_4_seconds_by_whether_a_trust_dialog_is_likely(int milliseconds, bool trust, string word)
    {
        var chip = SessionChips.Starting(TimeSpan.FromMilliseconds(milliseconds), trust);

        Assert.Equal(word, chip.Word);
    }

    [Fact]
    public void Answer_in_the_pane_fits_the_18_character_chip() =>
        Assert.Equal(18, SessionChips.Starting(TimeSpan.FromSeconds(5), trustDialogLikely: true).Word.Length);

    [Fact]
    public void The_starting_state_has_the_no_state_glyph_and_its_word() =>
        Assert.Equal(("○", "Starting"), (SessionChips.For("starting").Glyph, SessionChips.For("starting").Word));
}
