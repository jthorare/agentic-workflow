using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>What a refresh reads per level (#3801): one run's level reads only the run, and Back to the runs reads the list it skipped; the runs level still reads the list, whose row decides Activate and Pause.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsLevelReadsTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OnRun()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        return page;
    }

    [Fact]
    public async Task A_refresh_on_one_run_reads_the_run_and_not_the_list()
    {
        var page = await OnRun();
        var (lists, runs) = (_f.Server.WorkflowsCalls, _f.Server.WorkflowRunCalls);

        await page.Show(Url, Token);

        Assert.Equal((lists, runs + 1), (_f.Server.WorkflowsCalls, _f.Server.WorkflowRunCalls));
    }

    [Fact]
    public async Task Back_after_a_skipped_list_read_reads_the_list_and_the_buttons_follow_it()
    {
        var page = await OnRun();
        await page.Show(Url, Token);
        var lists = _f.Server.WorkflowsCalls;
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["activeRevision"] = null;   // paused elsewhere while the run was open
        _f.ServeList(list);

        page.Back();
        for (var i = 0; i < 200 && _f.Server.WorkflowsCalls == lists; i++)
        {
            await Task.Delay(10, Token);
        }

        Assert.Equal((lists + 1, true, true, false), (_f.Server.WorkflowsCalls, page.IsRuns, page.CanActivate, page.CanPause));
    }

    [Fact]
    public async Task Back_with_no_skipped_read_reads_nothing()
    {
        var page = await OnRun();
        var lists = _f.Server.WorkflowsCalls;

        page.Back();

        Assert.Equal(lists, _f.Server.WorkflowsCalls);
    }

    [Fact]
    public async Task A_refresh_on_the_runs_reads_the_list_and_the_runs()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        var (lists, runs) = (_f.Server.WorkflowsCalls, _f.Server.WorkflowRunsCalls);

        await page.Show(Url, Token);

        Assert.Equal((lists + 1, runs + 1), (_f.Server.WorkflowsCalls, _f.Server.WorkflowRunsCalls));
    }
}
