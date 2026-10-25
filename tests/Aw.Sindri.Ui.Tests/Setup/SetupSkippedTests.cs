using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>A skipped step is not marked like a done one (ux-pass B-18), and a web address in a step's words is offered as a link.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupSkippedTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_skipped_step_does_not_wear_the_tick_of_a_done_one()
    {
        _f.Serve("onyx-skipped");
        var steps = (await _f.Read(Token)).Steps;
        var onyx = steps.Single(s => s.Id == "onyx");

        Assert.Equal(("Skipped", true), (onyx.ChipWord, onyx.NumberText != "✓" && onyx.NumberText.Length > 0));
        Assert.All(steps.Where(s => s.IsDone), done => Assert.Equal("✓", done.NumberText));
    }

    [Theory]
    [InlineData("See https://example.com/docs for details.", "https://example.com/docs")]
    [InlineData("Open http://127.0.0.1:3000/, then sign in", "http://127.0.0.1:3000/")]
    public void A_web_address_in_the_words_is_found_without_the_punctuation_after_it(string text, string expected) =>
        Assert.Equal(new Uri(expected), SetupLinks.UrlIn(text));

    [Fact]
    public void Words_without_an_address_have_no_link() =>
        Assert.Null(SetupLinks.UrlIn("Onyx isn't set up."));
}
