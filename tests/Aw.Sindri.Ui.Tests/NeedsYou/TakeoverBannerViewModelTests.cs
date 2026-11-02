using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Takeover;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The takeover banner of a sandboxed run (sandbox spec §7, accessibility row 'Takeover banner'): golden text, read once, dismissible once, back on the next takeover.</summary>
[Lane(TestLane.Unit)]
public sealed class TakeoverBannerViewModelTests
{
    private const string Launch = "LCH-00000000000000000000000000000001";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static string Golden => GoldenFiles.Sandbox("takeover-banner.txt").Trim();

    [Fact]
    public void The_banner_says_the_golden_text_while_a_sandboxed_run_is_taken_over()
    {
        var banner = new TakeoverBannerViewModel();

        banner.Update(sandboxed: true, takenOver: true);

        Assert.Equal((Golden, true), (banner.Text, banner.IsVisible));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void It_does_not_show_for_a_run_that_is_not_sandboxed_or_not_taken_over(bool sandboxed, bool takenOver)
    {
        var banner = new TakeoverBannerViewModel();

        banner.Update(sandboxed, takenOver);

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void It_is_read_once_on_appearance_and_a_repeat_update_does_not_read_it_again()
    {
        var banner = new TakeoverBannerViewModel();
        banner.Update(sandboxed: true, takenOver: true);
        var changes = 0;
        banner.Voice.PropertyChanged += (_, _) => changes++;

        banner.Update(sandboxed: true, takenOver: true);

        Assert.Equal((Golden, true, 0), (banner.Voice.Announcement, banner.Voice.IsAnnouncing, changes));
    }

    [Fact]
    public void Dismiss_hides_it_for_good_until_the_run_is_handed_back_and_taken_over_again()
    {
        var banner = new TakeoverBannerViewModel();
        banner.Update(sandboxed: true, takenOver: true);

        banner.Dismiss();
        banner.Update(sandboxed: true, takenOver: true);
        Assert.False(banner.IsVisible);

        banner.Update(sandboxed: true, takenOver: false);
        banner.Update(sandboxed: true, takenOver: true);
        Assert.True(banner.IsVisible);
    }

    private TakeoverViewModel Takeover() => new(_server, () => _api, new InstanceCommander(_server, () => _api), _clock);

    private void ServeTakenOver(bool launched)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        var status = instance["current"]!["status"]!;
        status["paused"] = new JsonObject { ["timeoutLeftSeconds"] = 10200 };
        status["run"] = launched ? new JsonObject { ["runtime"] = "agent", ["launch"] = Launch } : null;
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
        _server.Launches[Launch] = RouteAnswer.SpecShaped("launches-id", "sandbox-started");
    }

    [Fact]
    public async Task A_taken_over_run_whose_launch_has_a_sandbox_shows_the_banner_on_the_card()
    {
        ServeTakenOver(launched: true);
        var takeover = Takeover();

        await takeover.Load(NeedsYouRows.DeskInstance);

        Assert.Equal((true, true), (takeover.Banner.IsVisible, takeover.Banner.Voice.IsAnnouncing));
    }

    [Fact]
    public async Task A_run_with_no_launch_shows_no_banner()
    {
        ServeTakenOver(launched: false);
        var takeover = Takeover();

        await takeover.Load(NeedsYouRows.DeskInstance);

        Assert.False(takeover.Banner.IsVisible);
    }

    [Fact]
    public async Task Handing_back_takes_the_banner_away()
    {
        ServeTakenOver(launched: true);
        var takeover = Takeover();
        await takeover.Load(NeedsYouRows.DeskInstance);

        await takeover.HandBack();

        Assert.False(takeover.Banner.IsVisible);
    }
}
