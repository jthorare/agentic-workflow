using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>F3 workflows spec §2, §4: the board's six lanes, the line workflow's states in them, and the empty-lane words.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowLanesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<WorkflowsViewModel> Shown()
    {
        var server = new FakeSindriApi { InstancesRewrite = LineGolden.AsLine };
        var list = new WorkflowsViewModel(server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await list.Refresh(_api, Token);
        return list;
    }

    [Theory]
    [InlineData("scoping", "Scoping")]
    [InlineData("design", "Design")]
    [InlineData("redesign", "Design")]
    [InlineData("plan", "Plan")]
    [InlineData("phase", "Build and prove")]
    public void A_line_state_lands_in_its_lane(string state, string lane) =>
        Assert.Equal(lane, LineStages.Name(LineStages.LaneOf(state)!.Value));

    [Theory]
    [InlineData("done")]
    [InlineData("stuck")]
    [InlineData("dropped")]
    [InlineData("mitigate")]
    [InlineData(null)]
    public void A_finished_state_or_no_line_stage_sits_in_no_open_lane(string? lineStage) =>
        Assert.Null(LineStages.LaneOf(lineStage));

    [Fact]
    public void The_board_has_six_lanes_in_order_even_when_nothing_is_loaded()
    {
        var list = new WorkflowsViewModel(new FakeSindriApi(), new FakeClipboard(), new FakeTimeProvider(A.Noon));

        Expect.Sequence(["Scoping", "Design", "Plan", "Build and prove", "Merge", "Done"], list.Lanes.Select(l => l.Name));
    }

    [Fact]
    public async Task Each_open_line_run_is_a_card_in_the_lane_of_its_state()
    {
        var list = await Shown();

        Assert.Equal(["ENG · Tab manager v2"], Cards(list, "Scoping"));
        Assert.Equal(["ENG · Billing exports"], Cards(list, "Design"));
        Assert.Equal(["APP · Offline sync"], Cards(list, "Build and prove"));
        Assert.Empty(Cards(list, "Plan"));
    }

    [Fact]
    public async Task Another_workflows_runs_are_not_on_the_board()
    {
        var list = await Shown();

        var onBoard = list.Lanes.SelectMany(l => l.Cards).Select(c => c.Recipe).Distinct(StringComparer.Ordinal);
        Assert.Equal("workflow.line", Assert.Single(onBoard));
    }

    [Fact]
    public async Task A_card_goes_to_the_lane_of_the_served_line_stage_not_of_its_own_state()
    {
        var server = new FakeSindriApi { InstancesRewrite = body => LineGolden.AsLine(body).Replace("\"lineStage\":\"scoping\"", "\"lineStage\":\"plan\"", StringComparison.Ordinal) };
        var list = new WorkflowsViewModel(server, new FakeClipboard(), new FakeTimeProvider(A.Noon));

        await list.Refresh(_api, Token);

        Assert.Equal(["ENG · Tab manager v2"], Cards(list, "Plan"));
        Assert.Empty(Cards(list, "Scoping"));
    }

    [Fact]
    public async Task Merge_stays_empty_and_teaches_what_waits_there()
    {
        var list = await Shown();

        var merge = list.Lanes.Single(l => l.Id == LaneId.Merge);
        Assert.True(merge.IsEmpty);
        Assert.Equal("Nothing here.", merge.EmptyTitle);
        Assert.Equal("Runs wait here for CI and docs, then for your approval.", merge.EmptyBody);
        Assert.Equal("No runs in this stage.", list.Lanes.Single(l => l.Id == LaneId.Plan).EmptyBody);
    }

    [Fact]
    public async Task The_done_lane_fills_from_the_finished_line_runs_and_reads_plain_when_empty()
    {
        var list = await Shown();

        var done = list.Lanes.Single(l => l.Id == LaneId.Done);
        Assert.Equal(4, done.Cards.Count);
        Assert.All(done.Cards, c => Assert.Equal("Done", c.Status.Word));
        Assert.Equal("No finished runs yet.", LineStages.EmptyBody(LaneId.Done));
    }

    [Fact]
    public async Task A_lane_header_counts_its_cards_and_names_them_for_screen_readers()
    {
        var list = await Shown();

        var done = list.Lanes.Single(l => l.Id == LaneId.Done);
        Assert.Equal("4", done.CountText);
        Assert.Equal("Done, 4 runs", done.AccessibleName);
    }

    [Fact]
    public async Task A_card_reads_the_time_in_stage_for_an_open_run_and_how_long_ago_for_a_finished_one()
    {
        var list = await Shown();

        Assert.StartsWith("in stage ", list.Lanes.Single(l => l.Id == LaneId.Scoping).Cards[0].TimeText, StringComparison.Ordinal);
        Assert.StartsWith("ended ", list.Lanes.Single(l => l.Id == LaneId.Done).Cards[0].TimeText, StringComparison.Ordinal);
        Assert.EndsWith(" ago", list.Lanes.Single(l => l.Id == LaneId.Done).Cards[0].TimeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Switching_to_the_list_and_back_keeps_the_lanes()
    {
        var list = await Shown();

        list.ToggleView();
        Assert.True(list.IsList);
        list.ToggleView();

        Assert.True(list.IsBoard);
        Assert.Equal(["ENG · Tab manager v2"], Cards(list, "Scoping"));
    }

    [Fact]
    public async Task The_keys_walk_the_cards_in_lane_order_on_the_board()
    {
        var list = await Shown();

        Assert.True(list.Press("J"));
        var first = list.SelectedRow!.Subject;
        Assert.True(list.Press("J"));

        Assert.Equal("ENG · Tab manager v2", first);
        Assert.Equal("ENG · Billing exports", list.SelectedRow!.Subject);
    }

    [Fact]
    public async Task V_switches_the_view()
    {
        var list = await Shown();

        Assert.True(list.Press("V"));
        Assert.True(list.IsList);
    }

    private static string[] Cards(WorkflowsViewModel list, string lane) => [.. list.Lanes.Single(l => l.Name == lane).Cards.Select(c => c.Subject)];
}
