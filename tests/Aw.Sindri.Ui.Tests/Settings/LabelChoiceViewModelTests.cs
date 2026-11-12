using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings' label-choice line (model routing spec 8.5): its three states in words from what Sindri serves, and the sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class LabelChoiceViewModelTests
{
    private static LabelChoiceViewModel Line(string labelPick)
    {
        var line = new LabelChoiceViewModel();
        line.Show(new ProfileView("b", Problem: null, Source: "/home/thor/profile.yaml", LabelPick: labelPick));
        return line;
    }

    [Fact]
    public void Until_the_first_status_the_line_says_off() => Assert.Equal("Label choice: off", new LabelChoiceViewModel().Line);

    [Theory]
    [InlineData("off", "Label choice: off")]
    [InlineData("needs-key", "Label choice: on, but needs an API key")]
    [InlineData("on", "Label choice: on")]
    public void The_line_follows_the_verdict_sindri_serves(string verdict, string expected) => Assert.Equal(expected, Line(verdict).Line);

    [Fact]
    public void A_verdict_the_app_does_not_know_reads_as_off() => Assert.Equal("Label choice: off", Line("maybe").Line);

    [Fact]
    public void A_status_without_the_field_reads_as_off() =>
        Assert.Equal("Label choice: off", Line(new ProfileView("b", Problem: null, Source: null).LabelPick).Line);

    [Fact]
    public void The_title_and_the_sentence_say_what_jev_reads_and_where_to_change_it()
    {
        var line = Line("on");

        Assert.Equal("Label choice", line.Title);
        Assert.Equal("A model reads each feature's file paths and the candidate names to pick the name shown. Change it in the profile; it takes effect when you save. Retention is the provider's.", line.Sentence);
    }
}
