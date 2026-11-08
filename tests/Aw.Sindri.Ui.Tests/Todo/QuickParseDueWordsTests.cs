using Aw.Kernel.Dates;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>
/// The quick-add box and the CLI agree on every day word (todo spec D7): each word runs through <see cref="TodoQuickParse"/> and through
/// <see cref="DueWords"/>. The word list is copied from DueWordsTests (that class keeps its cases inline, so there is no table to share).
/// Today is Wednesday 2026-11-11, as there.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class QuickParseDueWordsTests
{
    private static readonly DateOnly _today = new(2026, 11, 11);

    [Theory]
    [InlineData("today")]
    [InlineData("tomorrow")]
    [InlineData("fri")]
    [InlineData("mon")]
    [InlineData("wed")]
    [InlineData("sun")]
    [InlineData("Fri")]
    [InlineData("thurs")]
    [InlineData("thur")]
    [InlineData("tues")]
    [InlineData("weds")]
    [InlineData("Friday")]
    [InlineData("sunday")]
    [InlineData("11-20")]
    [InlineData("11-11")]
    [InlineData("11-10")]
    [InlineData("02-29")]
    [InlineData("2026-11-10")]
    public void A_word_the_CLI_reads_is_the_same_date_in_the_box(string word)
    {
        var expected = DueWords.Resolve(word, _today);
        Assert.True(expected.IsSuccess);

        var read = TodoQuickParse.Parse("call the dentist " + word, _today);

        Assert.Equal(("call the dentist", expected.Value), (read.Text, read.Due));
    }

    [Theory]
    [InlineData("frday")]
    [InlineData("11-31")]
    [InlineData("2026-13-01")]
    [InlineData("11-1")]   // one-digit parts: the CLI refuses them, so the box keeps them as text
    [InlineData("1-11")]
    public void A_word_the_CLI_refuses_stays_text_in_the_box(string word)
    {
        Assert.True(DueWords.Resolve(word, _today).IsError);

        var read = TodoQuickParse.Parse("call the dentist " + word, _today);

        Assert.Equal(("call the dentist " + word, null), (read.Text, read.Due));
    }
}
