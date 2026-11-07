using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Work page's Sindri / Linear switch (Linear hub spec §3): the tabs, their keys, the remembered pick and the C hint.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearWorkSwitchTests
{
    private readonly LinearFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Work_opens_on_the_Sindri_view()
    {
        var work = _f.Work();

        Assert.Equal((false, true), (work.IsLinear, work.IsSindri));
    }

    [Fact]
    public void L_toggles_between_the_views()
    {
        var work = _f.Work();

        work.Toggle();
        var afterOne = work.IsLinear;
        work.Toggle();

        Assert.Equal((true, false), (afterOne, work.IsLinear));
    }

    [Theory]
    [InlineData("Right", false, true)]
    [InlineData("Left", true, false)]
    [InlineData("End", false, true)]
    [InlineData("Home", true, false)]
    public void The_arrow_keys_Home_and_End_move_between_the_two_tabs(string key, bool startsOnLinear, bool endsOnLinear)
    {
        var work = _f.Work();
        work.Choose(startsOnLinear);

        var handled = work.MoveTab(key);

        Assert.Equal((true, endsOnLinear), (handled, work.IsLinear));
    }

    [Fact]
    public void Another_key_is_not_a_tab_key()
    {
        var work = _f.Work();

        Assert.Equal((false, false), (work.MoveTab("Up"), work.IsLinear));
    }

    [Fact]
    public async Task The_pick_is_remembered_in_the_preferences_and_read_back_on_the_next_start()
    {
        var work = _f.Work();
        work.Toggle();
        await _f.Prefs.Flush();

        var next = _f.Work();
        await next.Restore(Token);

        Assert.Equal((UiPreferences.LinearView, true), ((await _f.Prefs.Load(Token)).WorkView, next.IsLinear));
    }

    [Fact]
    public async Task Picking_Sindri_again_is_remembered_too()
    {
        var work = _f.Work();
        work.Choose(linear: true);
        work.Choose(linear: false);
        await _f.Prefs.Flush();

        Assert.Equal(UiPreferences.SindriView, (await _f.Prefs.Load(Token)).WorkView);
    }

    [Fact]
    public void The_C_hint_names_the_active_view()
    {
        var work = _f.Work();
        var sindri = work.CreateText;

        work.Choose(linear: true);

        Assert.Equal(("Create item", "New issue"), (sindri, work.CreateText));
    }

    [Fact]
    public async Task The_Linear_tab_carries_the_strips_count_in_its_text_and_in_its_name()
    {
        var work = await _f.Shown(Token);

        Assert.Equal(("Linear · 3 new", "Linear, 3 new"), (work.LinearTabText, work.LinearTabName));
    }

    [Fact]
    public async Task With_nothing_new_the_tab_is_just_Linear()
    {
        _f.Server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "empty");

        var work = await _f.Shown(Token);

        Assert.Equal(("Linear", "Linear"), (work.LinearTabText, work.LinearTabName));
    }

    [Fact]
    public async Task The_count_shows_on_the_tab_while_the_Sindri_view_is_picked()
    {
        var work = _f.Work();
        work.SetShown(shown: true);

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal(("Linear · 3 new", false), (work.LinearTabText, work.ShowsStrip));
        Assert.Empty(_f.Server.LinearQueries);
    }
}
