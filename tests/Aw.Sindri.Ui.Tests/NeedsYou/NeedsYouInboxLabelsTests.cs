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
            ["Your approvals", "Paused runs", "Agent decisions", "Workflow and settings approvals", "Something failed", "Your approval", "Paused run", "Agent decision", "Workflow change", "Settings change"],
            [Labels.NeedsYouGroupApprovals, Labels.NeedsYouGroupRuns, Labels.NeedsYouGroupDecisions, Labels.NeedsYouGroupSettings, Labels.NeedsYouGroupFailures, Labels.NeedsYouKindApproval, Labels.NeedsYouKindRun, Labels.NeedsYouKindDecision, Labels.NeedsYouKindWorkflow, Labels.NeedsYouKindSettings]);

    [Fact]
    public void The_page_and_card_words_are_plain() =>
        Assert.Equal(
            ["Needs you", "3 waiting, 1 run", "1 new request, held while you read", "2 new requests, held while you read", "Why this needs you", "Waiting for you", "waiting 5h 51m", "Open the item", "Nothing needs you"],
            [Labels.NeedsYouInboxTitle, Labels.NeedsYouWaiting(3, 1), Labels.NeedsYouNewPill(1), Labels.NeedsYouNewPill(2), Labels.NeedsYouWhyCaption, Labels.NeedsYouFootWaiting, Labels.NeedsYouFootRight("5h 51m"), Labels.NeedsYouOpenItem, Labels.NeedsYouEmptyFallback]);

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
        Assert.Equal(["0 waiting, 0 runs", "2 waiting, 2 runs", "Home, back to Home"], [Labels.NeedsYouWaiting(0, 0), Labels.NeedsYouWaiting(2, 2), Labels.NeedsYouCrumbHomeName]);

    [Fact]
    public void No_inbox_word_has_an_em_dash()
    {
        string[] words = [Labels.NeedsYouGroupSettings, Labels.NeedsYouNewPill(3), Labels.NeedsYouWhyCaption, Labels.NeedsYouCardName("Your approval", "Try the engine"), Labels.NeedsYouGroupName("Paused runs", "2")];

        Assert.DoesNotContain(words, w => w.Contains('—', StringComparison.Ordinal));
    }
}
