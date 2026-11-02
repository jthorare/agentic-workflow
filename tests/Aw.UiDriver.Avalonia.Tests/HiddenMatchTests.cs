using Avalonia.Automation;
using Avalonia.Controls;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>An act verb resolves among visible matches only; a selector that matches only hidden elements is AW-UIDRIVER-003; read verbs still list hidden ones (#1389).</summary>
public sealed class HiddenMatchTests
{
    private static readonly Selector _ship = new(Name: "Ship");

    // A hidden button named "Ship" ahead of a visible one with the same name, in tree order.
    private static Task<(CountingCommand Hidden, CountingCommand Shown)> AddPair(TestScreen screen, bool showSecond) => TestScreen.OnUi(() =>
    {
        var hidden = new CountingCommand();
        var shown = new CountingCommand();
        var first = new Button { Content = "Ship", Command = hidden, IsVisible = false };
        var second = new Button { Content = "Ship", Command = shown, IsVisible = showSecond };
        AutomationProperties.SetName(first, "Ship");
        AutomationProperties.SetName(second, "Ship");
        if (screen.Window.Content is StackPanel form)
        {
            form.Children.Insert(0, first);
            form.Children.Insert(1, second);
        }

        return (hidden, shown);
    });

    [Fact, Trait("Lane", "integration")]
    public async Task Click_skips_a_hidden_match_and_acts_on_the_visible_one()
    {
        var screen = await TestScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);
        var (hidden, shown) = await AddPair(screen, showSecond: true);

        var clicked = await host.Click(new ClickRequest(_ship));

        Assert.True(clicked.IsSuccess, clicked.IsError ? clicked.Error.Message : string.Empty);
        Assert.Equal((0, 1), (hidden.Runs, shown.Runs));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Index_counts_visible_matches_for_an_act_verb()
    {
        var screen = await TestScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);
        var (hidden, shown) = await AddPair(screen, showSecond: true);

        var past = await host.Click(new ClickRequest(_ship with { Index = 1 }));

        Assert.Equal(UiDriverErrors.NoMatch.Code, past.Error.Code);
        Assert.Equal((0, 0), (hidden.Runs, shown.Runs));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_selector_matching_only_hidden_elements_is_not_actionable()
    {
        var screen = await TestScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);
        var (hidden, shown) = await AddPair(screen, showSecond: false);

        var clicked = await host.Click(new ClickRequest(_ship));
        var typed = await host.Type(new TypeRequest("x", _ship, Replace: true));

        Assert.Equal((UiDriverErrors.NotActionable.Code, UiDriverErrors.NotActionable.Code), (clicked.Error.Code, typed.Error.Code));
        Assert.Contains("hidden", clicked.Error.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0), (hidden.Runs, shown.Runs));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Search_still_lists_hidden_and_visible_matches()
    {
        var screen = await TestScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);
        _ = await AddPair(screen, showSecond: true);

        var found = await host.Search(new SearchRequest(_ship));

        Assert.Equal(2, found.Value.Total);
        Assert.Equal([false, true], found.Value.Matches.Select(static m => m.Visible));
    }
}
