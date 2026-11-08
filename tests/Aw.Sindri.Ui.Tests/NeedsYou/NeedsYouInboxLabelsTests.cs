using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's words (needs-you spec §6): plain, no em dash, no internal term.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouInboxLabelsTests
{
    [Fact]
    public void The_group_and_kind_words_are_plain() =>
        Assert.Equal(
            ["Agent decisions", "Settings approvals", "Something failed", "Agent decision", "Settings change"],
            [Labels.NeedsYouGroupDecisions, Labels.NeedsYouGroupSettings, Labels.NeedsYouGroupFailures, Labels.NeedsYouKindDecision, Labels.NeedsYouKindSettings]);

    [Fact]
    public void The_page_and_card_words_are_plain() =>
        Assert.Equal(
            ["Needs you", "3 waiting", "1 new request, held while you read", "2 new requests, held while you read", "Why this needs you", "Waiting for you", "waiting 5h 51m", "Open the item", "Nothing needs you"],
            [Labels.NeedsYouInboxTitle, Labels.NeedsYouWaiting(3), Labels.NeedsYouNewPill(1), Labels.NeedsYouNewPill(2), Labels.NeedsYouWhyCaption, Labels.NeedsYouFootWaiting, Labels.NeedsYouFootRight("5h 51m"), Labels.NeedsYouOpenItem, Labels.NeedsYouEmptyFallback]);

    [Theory]
    [InlineData(0, "<1m")]
    [InlineData(26, "26m")]
    [InlineData(351, "5h 51m")]
    [InlineData(540, "9h 0m")]
    [InlineData(9720, "6d 18h")]
    public void How_long_a_row_has_waited_reads_to_the_minute(int minutes, string words) =>
        Assert.Equal(words, Labels.NeedsYouWaited(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void The_count_and_the_breadcrumb_read_right() =>
        Assert.Equal(["0 waiting", "2 waiting", "Home, back to Home"], [Labels.NeedsYouWaiting(0), Labels.NeedsYouWaiting(2), Labels.NeedsYouCrumbHomeName]);

    [Fact]
    public void No_inbox_word_has_an_em_dash()
    {
        string[] words = [Labels.NeedsYouGroupSettings, Labels.NeedsYouNewPill(3), Labels.NeedsYouWhyCaption, Labels.NeedsYouCardName("Settings change", "Try the engine"), Labels.NeedsYouGroupName("Agent decisions", "2")];

        Assert.DoesNotContain(words, w => w.Contains('—', StringComparison.Ordinal));
    }
}
