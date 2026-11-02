using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings' product-surface-choice line (model routing spec 8.5): its three states in words from what Sindri serves, and the sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class SurfaceChoiceViewModelTests
{
    private static SurfaceChoiceViewModel Line(string surfacePick)
    {
        var line = new SurfaceChoiceViewModel();
        line.Show(new ProfileView("a", "b", ChangedSinceApproval: false, Problem: null, Source: "/home/thor/profile.yaml", NeedsApproval: false, SurfacePick: surfacePick));
        return line;
    }

    [Fact]
    public void Until_the_first_status_the_line_says_off() => Assert.Equal("Product surface choice: off", new SurfaceChoiceViewModel().Line);

    [Theory]
    [InlineData("off", "Product surface choice: off")]
    [InlineData("needs-key", "Product surface choice: on, but needs an API key")]
    [InlineData("on", "Product surface choice: on")]
    public void The_line_follows_the_verdict_sindri_serves(string verdict, string expected) => Assert.Equal(expected, Line(verdict).Line);

    [Fact]
    public void A_verdict_the_app_does_not_know_reads_as_off() => Assert.Equal("Product surface choice: off", Line("maybe").Line);

    [Fact]
    public void A_status_without_the_field_reads_as_off() =>
        Assert.Equal("Product surface choice: off", Line(new ProfileView("a", "b", ChangedSinceApproval: false, Problem: null, Source: null, NeedsApproval: false).SurfacePick).Line);

    [Fact]
    public void The_title_and_the_sentence_say_what_jev_reads_and_where_to_change_it()
    {
        var line = Line("on");

        Assert.Equal("Product surface choice", line.Title);
        Assert.Equal("Jev reads each candidate's name, file paths and edges to decide whether lookalikes are one surface. Change it in the profile; it takes effect once you approve. Retention is the provider's.", line.Sentence);
    }
}
