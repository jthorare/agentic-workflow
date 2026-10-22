using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>The Jobs page's filters and the line above a table with nothing running (F3 jobs spec §3).</summary>
[Lane(TestLane.Unit)]
public sealed class JobsStatesTests
{
    private readonly JobsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Choosing_a_status_reads_the_list_with_the_served_state()
    {
        var page = await _f.Shown(Token);

        page.SelectedState = "Done";
        await page.CatchUp;

        Assert.Equal("succeeded", _f.Server.LastJobsQuery?.State);
        Assert.Null(_f.Server.LastJobsQuery?.Kind);
    }

    [Fact]
    public async Task Choosing_a_kind_reads_the_list_with_the_served_kind()
    {
        var page = await _f.Shown(Token);
        Assert.Contains("Echo job", page.KindChoices, StringComparer.Ordinal);

        page.SelectedKind = "Echo job";
        await page.CatchUp;

        Assert.Equal("EchoJob", _f.Server.LastJobsQuery?.Kind);
    }

    [Fact]
    public async Task All_states_is_no_filter()
    {
        var page = await _f.Shown(Token);
        page.SelectedState = "Failed";
        await page.CatchUp;

        page.SelectedState = page.StateChoices[0];
        await page.CatchUp;

        Assert.Null(_f.Server.LastJobsQuery?.State);
    }

    [Fact]
    public async Task A_table_of_ended_jobs_says_nothing_is_running()
    {
        _f.ServeList("by-kind");

        var page = await _f.Shown(Token);

        Assert.Equal((true, "No running jobs."), (page.IsNoneRunning, page.NoneRunningText));
    }

    [Fact]
    public async Task A_running_job_means_no_such_line()
    {
        var page = await _f.Shown(Token);

        Assert.False(page.IsNoneRunning);
    }
}
