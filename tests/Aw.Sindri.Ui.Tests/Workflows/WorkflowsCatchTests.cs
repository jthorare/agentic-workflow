using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Workflows list's answer to a 'workflow.instance-changed' frame: it reads the list again and says whether the instance's row reached the frame's state.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsCatchTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (string Address, string State) FirstRow()
    {
        var row = GoldenFiles.Node("workflow-instances", "open")["instances"]![0]!;
        return ((string)row["address"]!, (string)row["state"]!);
    }

    private async Task<WorkflowsViewModel> Shown()
    {
        var list = new WorkflowsViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        list.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests
        await list.Refresh(_api, Token);
        return list;
    }

    [Fact]
    public async Task A_row_in_the_frames_state_is_caught_up_after_one_read()
    {
        var list = await Shown();
        var (address, state) = FirstRow();
        var reads = _server.InstancesCalls;

        Assert.True(await list.Catch(_api, new InstanceChange(address, state, 10), Token));
        Assert.Equal(reads + 1, _server.InstancesCalls);
    }

    [Fact]
    public async Task A_row_still_in_the_old_state_is_not_caught_up()
    {
        var list = await Shown();
        var (address, _) = FirstRow();

        Assert.False(await list.Catch(_api, new InstanceChange(address, "a-state-the-view-has-not-reached", 10), Token));
    }

    [Fact]
    public async Task An_instance_no_row_shows_is_caught_up_since_there_is_nothing_to_wait_for()
    {
        var list = await Shown();

        Assert.True(await list.Catch(_api, new InstanceChange("ITM-NO-ROW/project/1", "build", 10), Token));
    }

    [Fact]
    public async Task No_frame_reads_the_list_again()
    {
        var list = await Shown();
        var reads = _server.InstancesCalls;

        Assert.True(await list.Catch(_api, change: null, Token));
        Assert.Equal(reads + 1, _server.InstancesCalls);
    }
}
