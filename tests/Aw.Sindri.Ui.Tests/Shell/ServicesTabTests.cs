using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

[Lane(TestLane.Unit)]
public sealed class ServicesTabTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_tab_counts_the_services_and_overview_is_the_one_shown_first()
    {
        var system = new SystemTestHarness();

        await system.Show();

        Assert.Equal(("Services (6)", "6 scheduled", true), (system.Vm.ServicesTabLabel, system.Vm.ServicesScheduled, system.Vm.ShowOverview));
        Assert.Equal(6, system.Vm.ServiceTable.Count);
        Assert.Equal(4, system.Vm.ServiceCard.Count);
    }

    [Fact]
    public async Task Choosing_a_tab_swaps_the_other()
    {
        var system = new SystemTestHarness();
        await system.Show();

        system.Vm.ShowServicesTab = true;
        Assert.False(system.Vm.ShowOverview);

        system.Vm.ShowOverview = true;
        Assert.False(system.Vm.ShowServicesTab);
    }

    [Fact]
    public async Task A_failed_last_run_says_so_and_a_service_that_has_not_run_is_scheduled()
    {
        var system = new SystemTestHarness("ran-failed");

        await system.Show();

        var heartbeat = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal));
        Assert.Equal((Labels.ServiceLastRunFailed, true, "every 5s"), (heartbeat.Status, heartbeat.HasFailed, heartbeat.Every));
        Assert.StartsWith("last run ", heartbeat.LastRun, StringComparison.Ordinal);
        var other = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "setup", StringComparison.Ordinal));
        Assert.Equal((Labels.ServiceScheduled, Labels.ServiceNotRun), (other.Status, other.LastRun));
    }

    [Fact]
    public async Task A_paused_service_says_paused_and_offers_resume()
    {
        var system = new SystemTestHarness("paused");

        await system.Show();

        var heartbeat = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal));
        Assert.Equal((Labels.ServicePaused, "Resume service"), (heartbeat.Status, heartbeat.Toggle!.Label));
        Assert.Equal("Pause service", system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "setup", StringComparison.Ordinal)).Toggle!.Label);
    }

    [Fact]
    public async Task A_service_with_a_run_in_flight_says_running_now()
    {
        var system = new SystemTestHarness("running-job");

        await system.Show();

        var stuck = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "stuck", StringComparison.Ordinal));
        Assert.Equal((Labels.ServiceRunning, (string)GoldenFiles.Node("services", "running-job")["services"]!.AsArray().Single(r => (string?)r!["job"] is not null)!["job"]!), (stuck.Status, stuck.JobId));
    }

    [Fact]
    public async Task A_known_service_says_what_it_does_and_an_unknown_one_says_nothing()
    {
        var system = new SystemTestHarness("running-job");

        await system.Show();

        Assert.NotEmpty(system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal)).What);
        Assert.Empty(system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "stuck", StringComparison.Ordinal)).What);
    }

    [Fact]
    public async Task Run_now_sends_the_run_route_once_for_that_service()
    {
        var system = new SystemTestHarness();
        await system.Show();
        var row = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "setup", StringComparison.Ordinal));

        await row.Run!.Press(Token);

        Assert.Equal(["setup"], system.Server.RunServiceCalls, StringComparer.Ordinal);
        Assert.True(row.Run.IsAccepted);
    }

    [Fact]
    public async Task Pause_sends_the_pause_route_and_resume_the_resume_route()
    {
        var paused = new SystemTestHarness();
        await paused.Show();
        var resumed = new SystemTestHarness("paused");
        await resumed.Show();

        await paused.Vm.ServiceTable.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal)).Toggle!.Press(Token);
        await resumed.Vm.ServiceTable.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal)).Toggle!.Press(Token);

        Assert.Equal(["heartbeat"], paused.Server.PauseServiceCalls, StringComparer.Ordinal);
        Assert.Empty(paused.Server.ResumeServiceCalls);
        Assert.Equal(["heartbeat"], resumed.Server.ResumeServiceCalls, StringComparer.Ordinal);
        Assert.Empty(resumed.Server.PauseServiceCalls);
    }

    [Fact]
    public async Task A_refused_run_shows_the_servers_code_on_the_row()
    {
        var system = new SystemTestHarness("running-job");
        system.Server.RunServiceAnswer = RouteAnswer.Problem("services-name-run", "already-running");
        await system.Show();
        var row = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "stuck", StringComparison.Ordinal));

        await row.Run!.Press(Token);

        Assert.Equal(("SND-JOB-409", true), (row.Run.ErrorCode, row.Run.IsFailed));
    }

    [Fact]
    public async Task A_row_keeps_its_run_control_across_the_re_reads_that_follow_every_event()
    {
        var system = new SystemTestHarness();
        await system.Show();
        var before = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "setup", StringComparison.Ordinal));
        var run = before.Run;

        await system.Refresh();

        var after = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "setup", StringComparison.Ordinal));
        Assert.Same(before, after);
        Assert.Same(run, after.Run);
    }

    [Fact]
    public async Task Only_the_actions_a_row_allows_are_offered()
    {
        var system = new SystemTestHarness("approved");
        await system.Show();

        var runOnly = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "context-map-refresh", StringComparison.Ordinal));
        var both = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "heartbeat", StringComparison.Ordinal));

        Assert.Equal((true, false), (runOnly.RunShown, runOnly.ToggleShown));
        Assert.Equal((true, true), (both.RunShown, both.ToggleShown));
    }
}
