using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Sandbox;
using Aw.Sindri.Ui.Views.Sandbox;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>The park card row of accessibility.json: when it appears from a user action, focus goes to its heading; when it appears by itself, focus stays put.</summary>
[Lane(TestLane.Integration)]
public sealed class SandboxParkFocusTests
{
    private readonly SandboxTestBed _bed = new();

    private static NeedsYouEntry Park(string id) => new(id, NeedsYouUrgency.Colour)
    {
        Kind = "park",
        Title = "Try the engine",
        Sandbox = new SandboxBlockerLineView(Code: "SND-LAUNCH-014", Reason: "no-bwrap", Source: null, Text: "This machine has no sandbox tool, so this Step can't run by itself.", Command: null, State: null),
    };

    private async Task<(NeedsYouViewModel List, NeedsYouPageViewModel Page)> Page(params NeedsYouEntry[] rows)
    {
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _bed.Clock);
        await list.Restore(SandboxTestBed.Token);
        list.Update(new NeedsYouFeed(rows, string.Empty));
        var page = new NeedsYouPageViewModel(list, _bed.Clock, new FakeClipboard(), new DeskReporter(_bed.Server, () => SandboxTestBed.Api)) { Sandbox = _bed.Check };
        return (list, page);
    }

    [Fact]
    public async Task Picking_a_sandbox_park_row_asks_for_focus_on_its_heading()
    {
        var (_, page) = await Page(new NeedsYouEntry("plain", NeedsYouUrgency.Colour), Park("park"));
        var asked = 0;
        page.ParkFocusRequested += (_, _) => asked++;

        page.Select("park");
        page.Select("plain");

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task A_refresh_that_brings_the_park_row_in_asks_for_nothing()
    {
        var (list, page) = await Page(new NeedsYouEntry("plain", NeedsYouUrgency.Colour));
        var asked = 0;
        page.ParkFocusRequested += (_, _) => asked++;

        list.Update(new NeedsYouFeed([Park("park"), new NeedsYouEntry("plain", NeedsYouUrgency.Colour)], string.Empty));

        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task The_card_puts_focus_on_its_heading_when_asked()
    {
        var onHeading = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SandboxParkFocusTests).Assembly).Dispatch(() =>
        {
            var card = new SandboxParkCard { DataContext = new SandboxParkCardViewModel(Park("p").Sandbox!, "Try the engine", _bed.Check) };
            var window = new Window { Width = 600, Height = 400, Content = card };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            card.FocusHeading();
            Dispatcher.UIThread.RunJobs();

            var focused = window.FocusManager?.GetFocusedElement() as Control;
            window.Close();
            return Task.FromResult(string.Equals(focused?.Name, "SandboxParkHeading", StringComparison.Ordinal));
        }, SandboxTestBed.Token);

        Assert.True(onHeading);
    }
}
