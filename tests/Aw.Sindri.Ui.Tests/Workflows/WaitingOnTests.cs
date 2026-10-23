using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Who the run waits on (api #754): the served waitingOn is the Waiting on word, on the list row and on the board card.</summary>
[Lane(TestLane.Unit)]
public sealed class WaitingOnTests
{
    private static InstanceRowViewModel Row(string? waitingOn) =>
        new(FixtureInstances.Rows[0] with { WaitingOn = waitingOn }, new FakeClipboard(), new FakeTimeProvider(A.Noon));

    [Theory]
    [InlineData("you", "You")]
    [InlineData("agent", "AI agent")]
    [InlineData("engine", "Sindri")]
    [InlineData("someone", "someone")]
    public void The_served_word_is_the_waiting_on_column(string served, string words) => Assert.Equal(words, Row(served).WaitingOn);

    [Fact]
    public void A_server_that_serves_none_keeps_the_word_worked_out_from_the_status()
    {
        var row = Row(waitingOn: null);

        Assert.Equal(row.Status.Kind == RunStatusKind.Running ? "AI agent" : row.WaitingOn, row.WaitingOn);
    }

    [Fact]
    public void The_golden_rows_carry_the_three_words_and_nothing_for_an_ended_run()
    {
        var words = FixtureInstances.Rows.Select(r => new InstanceRowViewModel(r, new FakeClipboard(), new FakeTimeProvider(A.Noon))).Select(r => r.WaitingOn).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Contains("You", words, StringComparer.Ordinal);
        Assert.Contains("Sindri", words, StringComparer.Ordinal);
        Assert.Contains(string.Empty, words, StringComparer.Ordinal);
    }
}
