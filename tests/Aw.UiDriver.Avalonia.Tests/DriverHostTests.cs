using Avalonia.Threading;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>The verbs on a real window under the headless platform: each one is called from outside the UI thread, as the server calls them.</summary>
public sealed class DriverHostTests
{
    private static DriverHost Host(TestScreen screen) => new(() => screen.Window, TimeProvider.System);

    [Fact, Trait("Lane", "integration")]
    public async Task The_tree_has_ids_types_automation_ids_and_the_text_each_element_shows()
    {
        var screen = await TestScreen.OpenAsync();

        var tree = (await Host(screen).Tree(new TreeRequest())).Value.Root;

        Assert.Equal(("0", "Window"), (tree.Id, tree.Type));
        var all = Flatten(tree).ToList();
        var save = Assert.Single(all, static n => n.AutomationId == "save");
        Assert.Equal(("Button", "Save", true, true), (save.Type, save.Text, save.Visible, save.Enabled));
        Assert.StartsWith("0/", save.Id, StringComparison.Ordinal);
        Assert.Equal("Hello, Ada", Assert.Single(all, static n => n.AutomationId == "greeting").Text);
        Assert.False(Assert.Single(all, static n => n.AutomationId == "off").Enabled);
        Assert.True(save.Width > 0 && save.Height > 0);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tree_stops_at_the_depth_asked_for_and_can_start_below_the_window()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        var shallow = (await host.Tree(new TreeRequest(Depth: 0))).Value.Root;
        var form = (await host.Tree(new TreeRequest(new Selector(Type: "StackPanel", Index: 0), Depth: 1))).Value.Root;

        Assert.Empty(shallow.Children);
        Assert.Equal("StackPanel", form.Type);
        Assert.All(form.Children, static child => Assert.Empty(child.Children));
        Assert.True(form.Children.Count >= 7);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_search_finds_elements_by_every_selector_field_and_counts_them_all()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        var buttons = (await host.Search(new SearchRequest(new Selector(Type: "Button")))).Value;
        var limited = (await host.Search(new SearchRequest(new Selector(Type: "TextBlock"), Limit: 3))).Value;
        var byText = (await host.Search(new SearchRequest(new Selector(TextContains: "Ada")))).Value;

        Assert.Equal(["save", "off"], buttons.Matches.Select(static m => m.AutomationId), StringComparer.Ordinal);
        Assert.Equal((3, true), (limited.Matches.Count, limited.Total > 3));
        Assert.Contains(byText.Matches, static m => m.AutomationId == "greeting");
        Assert.All(buttons.Matches, static m => Assert.Empty(m.Children));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Props_lists_the_registered_properties_as_text()
    {
        var screen = await TestScreen.OpenAsync();

        var props = (await Host(screen).Props(new PropsRequest(new Selector(AutomationId: "greeting")))).Value;

        Assert.Equal("Hello, Ada", props.Properties["Text"]);
        Assert.Equal("TextBlock", props.Node.Type);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_selector_that_names_nothing_or_matches_nothing_is_refused_with_its_code()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        var empty = await host.Click(new ClickRequest(new Selector()));
        var missing = await host.Click(new ClickRequest(new Selector(AutomationId: "nope")));

        Assert.Equal(UiDriverErrors.Invalid.Code, empty.Error.Code);
        Assert.Equal((UiDriverErrors.NoMatch.Code, "no element matches automationId 'nope'"), (missing.Error.Code, missing.Error.Message));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_click_runs_the_buttons_command_and_flips_a_toggle()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        var saved = await host.Click(new ClickRequest(new Selector(AutomationId: "save")));
        var toggled = await host.Click(new ClickRequest(new Selector(AutomationId: "dark")));

        Assert.True(saved.IsSuccess && toggled.IsSuccess, saved.IsError ? saved.Error.Message : toggled.IsError ? toggled.Error.Message : string.Empty);
        Assert.Equal(1, screen.Saved.Runs);
        Assert.True(await TestScreen.OnUi(() => screen.Dark.IsChecked) ?? false);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_disabled_element_cannot_be_clicked()
    {
        var screen = await TestScreen.OpenAsync();

        var result = await Host(screen).Click(new ClickRequest(new Selector(AutomationId: "off")));

        Assert.Equal(UiDriverErrors.NotActionable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Typing_appends_to_a_text_box_or_replaces_what_it_holds()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        _ = await host.Type(new TypeRequest(" Lovelace", new Selector(AutomationId: "name")));
        var appended = await TestScreen.OnUi(() => screen.NameBox.Text);
        _ = await host.Type(new TypeRequest("Grace", new Selector(AutomationId: "name"), Replace: true));
        var replaced = await TestScreen.OnUi(() => screen.NameBox.Text);
        var notABox = await host.Type(new TypeRequest("x", new Selector(AutomationId: "greeting")));

        Assert.Equal(("Ada Lovelace", "Grace", UiDriverErrors.NotActionable.Code), (appended, replaced, notABox.Error.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_key_chord_is_raised_on_the_element_and_a_bad_chord_or_key_is_refused()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        var pressed = await host.Key(new KeyRequest("Ctrl+K", new Selector(AutomationId: "listener")));
        var badChord = await host.Key(new KeyRequest("Hyper+K", new Selector(AutomationId: "listener")));
        var badKey = await host.Key(new KeyRequest("Banana", new Selector(AutomationId: "listener")));

        Assert.True(pressed.IsSuccess, pressed.IsError ? pressed.Error.Message : string.Empty);
        Assert.Equal(["Control+K"], screen.Keys);
        Assert.Equal((UiDriverErrors.Invalid.Code, UiDriverErrors.Invalid.Code), (badChord.Error.Code, badKey.Error.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Scrolling_moves_the_scroll_viewer_and_stops_at_its_end()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        _ = await host.Scroll(new ScrollRequest(0, 60, new Selector(AutomationId: "list")));
        var first = await TestScreen.OnUi(() => screen.List.Offset.Y);
        _ = await host.Scroll(new ScrollRequest(0, 100_000, new Selector(AutomationId: "list")));
        var (offset, max) = await TestScreen.OnUi(() => (screen.List.Offset.Y, screen.List.Extent.Height - screen.List.Viewport.Height));

        Assert.Equal(60, first);
        Assert.Equal(max, offset);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_wait_holds_when_the_state_arrives_and_times_out_when_it_does_not()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);
        _ = Task.Delay(150, TestContext.Current.CancellationToken).ContinueWith(_ => Dispatcher.UIThread.Post(() => screen.Greeting.Text = "Done"), TaskScheduler.Default);

        var arrived = await host.WaitFor(new WaitForRequest(new Selector(AutomationId: "greeting"), "textEquals", "Done", 5000), TestContext.Current.CancellationToken);
        var timedOut = await host.WaitFor(new WaitForRequest(new Selector(AutomationId: "save"), "gone", TimeoutMs: 200), TestContext.Current.CancellationToken);
        var gone = await host.WaitFor(new WaitForRequest(new Selector(AutomationId: "absent"), "gone", TimeoutMs: 200), TestContext.Current.CancellationToken);
        var badState = await host.WaitFor(new WaitForRequest(new Selector(AutomationId: "save"), "soon"), TestContext.Current.CancellationToken);

        Assert.Equal("Done", arrived.Value.Node?.Text);
        Assert.Equal(UiDriverErrors.TimedOut.Code, timedOut.Error.Code);
        Assert.Null(gone.Value.Node);
        Assert.Equal(UiDriverErrors.Invalid.Code, badState.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_window_size_is_read_and_set()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);

        var before = (await host.WindowSize(new WindowRequest())).Value;
        var after = (await host.WindowSize(new WindowRequest(500, 350))).Value;
        var refused = await host.WindowSize(new WindowRequest(0, 10));

        Assert.Equal((400, 300, "Test screen"), (before.Width, before.Height, before.Title));
        Assert.Equal((500, 350), (after.Width, after.Height));
        Assert.Equal(UiDriverErrors.Invalid.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_screenshot_is_a_png_of_the_window_or_of_one_element()
    {
        var screen = await TestScreen.OpenAsync();
        var host = Host(screen);
        byte[] signature = [0x89, 0x50, 0x4E, 0x47];

        var window = (await host.Screenshot(new ScreenshotRequest())).Value;
        var element = (await host.Screenshot(new ScreenshotRequest(new Selector(AutomationId: "save")))).Value;

        Assert.Equal(signature, window[..4]);
        Assert.Equal(signature, element[..4]);
        Assert.True(window.Length > element.Length);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_a_window_every_verb_says_so()
    {
        var host = new DriverHost(() => null, TimeProvider.System);

        var result = await host.Tree(new TreeRequest());

        Assert.Equal((UiDriverErrors.Failed.Code, "the app has no window yet"), (result.Error.Code, result.Error.Message));
    }

    private static IEnumerable<NodeInfo> Flatten(NodeInfo node) => new[] { node }.Concat(node.Children.SelectMany(Flatten));
}
