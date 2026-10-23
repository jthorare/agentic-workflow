using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The row's served timeout on the inbox card's footer (needs-you spec §8 slice 5): "pauses and asks again in 6d 18h", counted down from when the list was read.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRowTimeoutTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouList Served(string scenario) =>
        AwJson.Deserialize<NeedsYouList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("needs-you", scenario)), SindriApiClient.Json).Value;

    private async Task<NeedsYouPageViewModel> Page(NeedsYouEntry row)
    {
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);
        await list.Restore(Token);
        list.Update(new NeedsYouFeed([row], ""));
        list.Select(row.Id);
        return new NeedsYouPageViewModel(list, _clock, new FakeClipboard(), new DeskReporter(_server, () => new Uri("http://127.0.0.1:7410/")));
    }

    [Fact]
    public void The_mapper_dates_the_served_timeout_from_the_moment_the_list_was_read()
    {
        var list = Served("park-with-timeout");
        var served = list.Rows.Single(r => r.Timeout is not null);

        var entry = NeedsYouMapper.Map(list, new CommandList([]), decisions: null, new HomeCounts(0, 0, 0), readAt: A.Noon).Rows.Single(r => r.Id == served.Id);

        Assert.Equal(new RowTimeout(served.Timeout!.Kind, A.Noon.AddSeconds(served.Timeout.InSeconds)), entry.Timeout);
    }

    [Fact]
    public void A_row_without_a_served_timeout_has_none()
    {
        var list = Served("park-with-timeout");
        var plain = list.Rows[0] with { Timeout = null };

        var entry = NeedsYouMapper.Map(list with { Rows = [plain] }, new CommandList([]), decisions: null, new HomeCounts(0, 0, 0), readAt: A.Noon).Rows[0];

        Assert.Null(entry.Timeout);
    }

    [Theory]
    [InlineData(true, "waiting 12m · pauses and asks again in 6d 18h")]
    [InlineData(false, "waiting 12m · escalates in 6d 18h")]
    public async Task The_footer_says_what_the_timeout_does_and_when(bool parks, string expected)
    {
        var page = await Page(NeedsYouRows.Park() with { Timeout = new RowTimeout(parks ? TimeoutKind.Parks : TimeoutKind.Escalates, A.Noon.AddDays(6).AddHours(18)) });

        Assert.Equal(expected, page.FooterRight);
        Assert.Equal("Waiting for you, " + expected, page.FooterName);
    }

    [Fact]
    public async Task The_time_left_counts_down_with_the_clock_and_stops_at_zero()
    {
        var page = await Page(NeedsYouRows.Park() with { Timeout = new RowTimeout(TimeoutKind.Parks, A.Noon.AddMinutes(30)) });
        Assert.EndsWith("asks again in 30m", page.FooterRight, StringComparison.Ordinal);

        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.EndsWith("asks again in 20m", page.FooterRight, StringComparison.Ordinal);

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.EndsWith("asks again in 0s", page.FooterRight, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_row_with_no_timeout_keeps_the_plain_footer()
    {
        var page = await Page(NeedsYouRows.Park());

        Assert.Equal("waiting 12m", page.FooterRight);
    }

    [Fact]
    public async Task A_desk_row_that_serves_its_stop_names_the_approval_and_the_stop_in_the_card_s_kind_word()
    {
        var served = Served("desk-with-stop");
        var entry = NeedsYouMapper.Map(served, AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value, decisions: null, new HomeCounts(0, 0, 0), readAt: A.Noon).Rows.Single(r => r.Id == served.Rows[0].Id);

        Assert.Equal(new NeedsYouStopView(2, 3, "checks"), entry.Stop);
        var page = await Page(entry);

        Assert.Equal("Approval 2 of 3 · Checks", page.KindWord);
    }

    [Fact]
    public async Task A_row_with_no_stop_keeps_the_plain_kind_word()
    {
        var served = Served("desk-with-stop");
        var entry = NeedsYouMapper.Map(served with { Rows = [served.Rows[0] with { Stop = null }] }, new CommandList([]), decisions: null, new HomeCounts(0, 0, 0), readAt: A.Noon).Rows[0];

        var page = await Page(entry);

        Assert.DoesNotContain("Approval 2", page.KindWord, StringComparison.Ordinal);
    }
}
