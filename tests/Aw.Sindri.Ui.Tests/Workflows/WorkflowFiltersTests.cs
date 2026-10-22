using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>F3 workflows spec §3, §4: All, Needs you and Mine, their counts from the rows loaded, and the search.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowFiltersTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<WorkflowsViewModel> Shown()
    {
        var list = new WorkflowsViewModel(new FakeSindriApi(), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        list.SetViewMode(WorkflowViewMode.List);
        await list.Refresh(_api, Token);
        return list;
    }

    private static string[] Words(WorkflowsViewModel list) => [.. list.Groups.SelectMany(g => g.Rows).Select(r => r.Chip.Word).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task All_shows_every_loaded_run()
    {
        var list = await Shown();

        Assert.Equal(8, list.Groups.Sum(g => g.Rows.Count));
        Assert.Equal("All 8", list.FilterAllText);
    }

    [Fact]
    public async Task Needs_you_is_the_decision_paused_and_escalated_runs()
    {
        var list = await Shown();

        list.SetFilter(WorkflowFilter.NeedsYou);

        Assert.Equal(["DECISION", "ESCALATED"], Words(list));
        Assert.Equal("Needs you 2", list.FilterNeedsYouText);
    }

    [Fact]
    public async Task Mine_adds_the_runs_you_took_over()
    {
        var list = await Shown();

        list.SetFilter(WorkflowFilter.Mine);

        Assert.Equal(["DECISION", "ESCALATED", "WITH YOU"], Words(list));
        Assert.Equal("Mine 3", list.FilterMineText);
    }

    [Fact]
    public async Task The_counts_do_not_change_with_the_filter()
    {
        var list = await Shown();

        list.SetFilter(WorkflowFilter.NeedsYou);

        Assert.Equal("All 8", list.FilterAllText);
        Assert.Equal("Mine 3", list.FilterMineText);
    }

    [Theory]
    [InlineData("tab manager", 1)]
    [InlineData("TAB MANAGER", 1)]
    [InlineData("ITM-", 8)]
    [InlineData("no such run", 0)]
    public async Task Search_matches_the_title_the_step_and_the_reference(string text, int expected)
    {
        var list = await Shown();

        list.SetSearch(text);

        Assert.Equal(expected, list.Groups.Sum(g => g.Rows.Count));
    }

    [Fact]
    public async Task A_filter_that_hides_every_run_says_so_in_every_lane()
    {
        var list = new WorkflowsViewModel(new FakeSindriApi { InstancesRewrite = LineGolden.AsLine }, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await list.Refresh(_api, Token);

        list.SetSearch("no such run");

        Assert.True(list.NoRunsMatch);
        Assert.Equal("Nothing here. No runs match this filter.", list.NoRunsMatchText);
        Assert.All(list.Lanes, lane => Assert.True(lane.IsEmpty));
    }

    [Fact]
    public async Task Clearing_the_search_brings_every_run_back()
    {
        var list = await Shown();
        list.SetSearch("tab manager");

        list.SetSearch("  ");

        Assert.Equal(8, list.Groups.Sum(g => g.Rows.Count));
        Assert.False(list.NoRunsMatch);
    }

    [Fact]
    public async Task Changing_the_filter_never_reads_the_list_again()
    {
        var server = new FakeSindriApi();
        var list = new WorkflowsViewModel(server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await list.Refresh(_api, Token);
        var reads = server.InstancesCalls;

        list.SetFilter(WorkflowFilter.Mine);
        list.SetSearch("x");

        Assert.Equal(reads, server.InstancesCalls);
    }
}
