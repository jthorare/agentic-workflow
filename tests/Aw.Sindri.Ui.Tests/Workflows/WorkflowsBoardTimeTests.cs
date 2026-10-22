using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The board's time words move with the clock, and the board says so when open runs have no lane.</summary>
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
    public async Task Open_runs_with_no_lane_are_counted_under_the_board()
    {
        var (list, _) = await Shown(LineGolden.AsLine);
        var open = list.Groups.Sum(g => g.Rows.Count);
        var onBoard = list.Lanes.Where(l => l.Id != LaneId.Done).Sum(l => l.Cards.Count);
        var off = open - onBoard;

        Assert.True(off > 1);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{off} runs not on the board — switch to list"), list.OffBoardText);
        Assert.True(list.HasOffBoard);
        Assert.False(list.IsBoardEmpty);
    }

    [Fact]
    public async Task No_run_on_any_lane_is_an_empty_board_that_points_at_the_list()
    {
        var (list, _) = await Shown(StripLineStage);

        Assert.True(list.IsBoardEmpty);
        Assert.False(list.ShowBoard);
        Assert.Equal("Nothing here.", list.BoardEmptyText);
        Assert.Equal("8 runs not on the board — switch to list", list.OffBoardText);
        Assert.False(list.IsEmpty);
    }

    [Fact]
    public async Task The_list_does_not_show_the_off_board_line()
    {
        var (list, _) = await Shown(StripLineStage);

        list.SetViewMode(WorkflowViewMode.List);

        Assert.False(list.HasOffBoard);
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
