using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>A service the scheduler really missed (#1226, #1091) shows as Overdue, not Scheduled, and the overview stops saying everything is running.</summary>
[Lane(TestLane.Unit)]
public sealed class OverdueServicesTests
{
    private const string Missed = "2026-10-11T11:55:00.0000000Z";   // five minutes before A.Noon

    [Fact]
    public async Task A_row_with_an_overdue_slot_says_overdue_and_when_it_missed_its_run()
    {
        var system = Harness(Row("heartbeat", overdueSince: Missed), Row("setup", overdueSince: null));

        await system.Show();

        var heartbeat = Named(system, "heartbeat");
        var missed = A.Noon.AddMinutes(-5).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        Assert.Equal((Labels.ServiceOverdue, true, $"missed its {missed} run"), (heartbeat.Status, heartbeat.IsOverdue, heartbeat.OverdueDetail));
        var setup = Named(system, "setup");
        Assert.Equal((Labels.ServiceScheduled, false, string.Empty), (setup.Status, setup.IsOverdue, setup.OverdueDetail));
    }

    [Fact]
    public async Task The_overview_names_the_overdue_service_instead_of_saying_everything_is_running()
    {
        var system = Harness(Row("heartbeat", overdueSince: Missed), Row("setup", overdueSince: null));

        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });

        Assert.Equal("Something needs a look: heartbeat overdue.", system.Vm.Sentence);
        Assert.True(system.Vm.ToneWarn);
        Assert.False(system.Vm.ToneError);
    }

    [Fact]
    public async Task With_no_overdue_service_the_overview_still_says_everything_is_running()
    {
        var system = Harness(Row("heartbeat", overdueSince: null));

        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });

        Assert.Equal(Labels.AllRunning, system.Vm.Sentence);
        Assert.False(system.Vm.ToneWarn);
    }

    private static SystemTestHarness Harness(params string[] rows)
    {
        var system = new SystemTestHarness();
        system.Server.ServiceView = new RouteAnswer(HttpStatusCode.OK,
            $$$"""{"services":[{{{string.Join(',', rows)}}}],"empty":null,"asOf":{"cursor":7,"ledgerHead":7,"projectedAt":"2026-01-01T00:00:00.0000000Z","rebuilding":false,"stalled":false}}""");
        return system;
    }

    private static string Row(string name, string? overdueSince)
    {
        var overdue = overdueSince is null ? string.Empty : ",\"overdueSince\":\"" + overdueSince + "\"";
        return "{\"name\":\"" + name + "\",\"every\":\"5s\",\"paused\":false,\"lastRun\":null,\"nextSlot\":null,\"job\":null,\"actions\":[\"run\",\"pause\"]" + overdue + "}";
    }

    private static ViewModels.Shell.ServiceRowViewModel Named(SystemTestHarness system, string name) =>
        system.Vm.ServiceTable.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal));
}
