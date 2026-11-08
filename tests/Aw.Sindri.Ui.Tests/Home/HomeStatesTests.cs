using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>Home's states (F3 home spec §3): skeleton rows before the first read, then each panel's empty line, the served sentence first and the spec's own words when none is served.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeStatesTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private NeedsYouViewModel NeedsYou() => new(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);

    [Fact]
    public void Needs_you_shows_skeleton_rows_and_no_empty_line_before_the_first_read()
    {
        var list = NeedsYou();

        Assert.Equal((true, string.Empty), (list.IsLoading, list.EmptyLine));
    }

    [Fact]
    public void Needs_you_with_nothing_served_says_nothing_needs_you()
    {
        var list = NeedsYou();

        list.Update(new NeedsYouFeed([], string.Empty));

        Assert.Equal((false, Labels.HomeNothingNeedsYou), (list.IsLoading, list.EmptyLine));
    }

    [Fact]
    public void Needs_you_keeps_the_sentence_the_api_served()
    {
        var list = NeedsYou();

        list.Update(new NeedsYouFeed([], "Nothing needs you right now."));

        Assert.Equal("Nothing needs you right now.", list.EmptyLine);
    }
}
