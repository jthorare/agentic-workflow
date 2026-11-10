using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The views banner's live regions (WCAG 4.1.3; UI spec §4.1, sindri-ui.md "LiveSetting on macOS"): flag first, then text; never empty.</summary>
[Lane(TestLane.Unit)]
public sealed class ViewsBannerTests
{
    private static ServiceList View(string scenario) =>
        AwJson.Deserialize<ServiceList>(Encoding.UTF8.GetBytes(RouteAnswer.SpecShaped("services", scenario).Body), SindriApiClient.Json).Value;

    [Fact]
    public void A_stalled_view_announces_its_sentence_after_the_flag_rises()
    {
        var banner = new ViewsBanner();
        var seen = new List<string>();
        banner.PropertyChanged += (_, e) => seen.Add(e.PropertyName ?? string.Empty);

        banner.Show(View("stalled"), rebuild: null);

        Assert.Equal("Views are stalled at 42: run `sindri views rebuild`", banner.Announcement);
        Assert.True(seen.IndexOf(nameof(ViewsBanner.IsShown)) < seen.IndexOf(nameof(ViewsBanner.Announcement)), "the flag changes before the text");
    }

    // With no Rebuild button the command is the only way to rebuild, so the shown text keeps it.
    [Fact]
    public void A_stalled_view_without_a_button_shows_the_command()
    {
        var banner = new ViewsBanner();

        banner.Show(View("stalled"), rebuild: null);

        Assert.Equal("Views are stalled at 42: run `sindri views rebuild`", banner.ShownText);
    }

    // The rebuilding sentence carries the cursor, which moves on every read: the live region says the state once.
    [Fact]
    public void Rebuilding_is_announced_once_not_on_every_cursor_move()
    {
        var banner = new ViewsBanner();
        var heard = new List<string>();
        banner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewsBanner.Announcement))
            {
                heard.Add(banner.Announcement);
            }
        };

        banner.Show(View("rebuilding"), rebuild: null);
        banner.Show(View("rebuilding") with { Empty = "Rebuilding views (cursor 20 of 42)…" }, rebuild: null);

        Assert.Equal(["Refreshing the lists"], heard);
    }

    [Fact]
    public void The_banner_going_away_after_trouble_announces_the_recovery_and_rests_on_a_non_empty_text()
    {
        var banner = new ViewsBanner();
        banner.Show(View("stalled"), rebuild: null);

        banner.Show(view: null, rebuild: null);

        Assert.Equal(("Sindri", true, "Lists are up to date"), (banner.Announcement, banner.IsRecovered, banner.RecoveredAnnouncement));

        banner.Show(View("stalled"), rebuild: null);
        Assert.False(banner.IsRecovered);
    }

    [Fact]
    public void Never_having_shown_announces_nothing()
    {
        var banner = new ViewsBanner();

        banner.Show(view: null, rebuild: null);

        Assert.Equal((false, "Sindri"), (banner.IsRecovered, banner.RecoveredAnnouncement));
    }
}
