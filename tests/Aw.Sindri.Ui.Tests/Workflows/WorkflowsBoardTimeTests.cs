using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The board's time words move with the clock, and the board draws open runs that have no lane in a lane of their own.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsBoardTimeTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(WorkflowsViewModel List, FakeTimeProvider Clock)> Shown(Func<string, string>? rewrite)
    {
        var clock = new FakeTimeProvider(A.Noon);
        var list = new WorkflowsViewModel(new FakeSindriApi { InstancesRewrite = rewrite }, new FakeClipboard(), clock);
        await list.Refresh(_api, Token);
        return (list, clock);
    }

    [Fact]
    public void A_row_served_with_a_new_since_is_not_the_same_row()
    {
        var served = FixtureInstances.Root.Instances[0];
        var a = new InstanceRowViewModel(served, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        var b = new InstanceRowViewModel(served with { Since = served.Since.AddHours(-1) }, new FakeClipboard(), new FakeTimeProvider(A.Noon));

        Assert.NotEqual(a.Signature, b.Signature, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_minute_of_clock_re_words_the_time_in_stage_of_every_row()
    {
        var (list, clock) = await Shown(LineGolden.AsLine);
        var card = list.Lanes.Single(l => l.Id == LaneId.Scoping).Cards[0];
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var before = card.TimeText;

        clock.Advance(TimeSpan.FromDays(400));   // the golden's since is later than the fake noon, so the age reads 0s until the clock passes it

        Assert.Contains(nameof(InstanceRowViewModel.TimeText), changed, StringComparer.Ordinal);
        Assert.NotEqual(before, card.TimeText, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Open_runs_outside_the_line_are_cards_in_a_lane_of_their_own_before_Done()
    {
        var (list, _) = await Shown(LineGolden.AsLine);
        var open = list.Groups.Sum(g => g.Rows.Count);
        var inLine = list.Lanes.Where(l => l.Id is not (LaneId.Done or LaneId.Other)).Sum(l => l.Cards.Count);
        var other = list.Lanes.Single(l => l.Id == LaneId.Other);

        Assert.Equal("Not on a line", other.Name);
        Assert.True(other.Cards.Count > 1);
        Assert.Equal(open, inLine + other.Cards.Count);
        Assert.Equal(LaneId.Done, list.Lanes[^1].Id);
        Assert.Equal(LaneId.Other, list.Lanes[^2].Id);
        Assert.False(list.IsBoardEmpty);
    }

    [Fact]
    public async Task A_board_with_no_run_on_the_line_still_draws_every_open_run()
    {
        var (list, _) = await Shown(StripLineStage);

        Assert.False(list.IsBoardEmpty);
        Assert.True(list.ShowBoard);
        Assert.Equal(8, list.Lanes.Single(l => l.Id == LaneId.Other).Cards.Count);
        Assert.Equal("All 8", list.FilterAllText);
        Assert.False(list.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task The_chips_count_the_cards_the_board_draws_for_each_filter(int segment)
    {
        var (list, _) = await Shown(StripLineStage);
        var filter = (WorkflowFilter)segment;

        list.SetFilter(filter);

        var chip = filter switch
        {
            WorkflowFilter.All => list.FilterAllText,
            WorkflowFilter.NeedsYou => list.FilterNeedsYouText,
            WorkflowFilter.Mine => list.FilterMineText,
            _ => string.Empty,
        };
        var drawn = list.Lanes.Where(l => l.Id != LaneId.Done).Sum(l => l.Cards.Count);
        Assert.EndsWith(" " + drawn.ToString(CultureInfo.InvariantCulture), chip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_does_not_draw_the_lane_for_runs_outside_the_line()
    {
        var (list, _) = await Shown(StripLineStage);

        list.SetViewMode(WorkflowViewMode.List);

        Assert.False(list.IsBoardEmpty);
        Assert.False(list.ShowBoard);
    }

    private static string StripLineStage(string body)
    {
        var list = JsonNode.Parse(body)!;
        foreach (var row in list["instances"]!.AsArray().Select(r => r!.AsObject()))
        {
            _ = row.Remove("lineStage");
        }

        return list.ToJsonString();
    }
}
