using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>A service row says what the service does from the server's own description, and why it is paused or failed, in plain words and with no internal ids (#1282).</summary>
[Lane(TestLane.Unit)]
public sealed class ServiceWhyTests
{
    private static readonly DateTimeOffset _pausedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_servers_description_wins_over_the_apps_own_table()
    {
        var system = new SystemTestHarness();
        system.Server.ServiceView = RouteAnswer.Api("services", "paused");

        await system.Show();

        Assert.Equal(
            ("Scans each new context map version for the concepts the code names, for the glossary.", "Embeds the code-index chunks that are not embedded yet."),
            (Named(system, "context-concept-scan").What, Named(system, "code-index-embed").What));
    }

    [Fact]
    public async Task A_service_paused_by_the_human_says_paused_by_you_and_when()
    {
        var system = new SystemTestHarness();
        system.Server.ServiceView = RouteAnswer.Api("services", "paused");

        await system.Show();

        Assert.Equal($"Paused by you, {Clock(_pausedAt)}", Named(system, "heartbeat").Why);
    }

    [Fact]
    public async Task A_service_paused_by_a_session_says_a_session_and_hides_its_id()
    {
        var system = Harness(Row("heartbeat", "\"paused\":true,\"pausedBy\":\"session:abc123\",\"pausedAt\":\"2026-01-01T00:00:00.0000000Z\""));

        await system.Show();

        var why = Named(system, "heartbeat").Why;
        Assert.Equal(($"Paused by a session, {Clock(_pausedAt)}", false), (why, why.Contains("abc123", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_failed_service_says_its_last_runs_detail_without_the_error_code()
    {
        var system = Harness(Row("heartbeat",
            "\"lastRun\":{\"slot\":\"2026-01-01T00:00:00.0000000Z\",\"outcome\":\"failed\",\"code\":\"SND-SCHED-001\",\"ranAt\":\"2026-01-01T00:00:00.0000000Z\",\"detail\":\"The forge answered 503 Service Unavailable.\"}"));

        await system.Show();

        var why = Named(system, "heartbeat").Why;
        Assert.Equal(("Last run failed: The forge answered 503 Service Unavailable.", false), (why, why.Contains("SND-", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_failed_service_with_no_detail_says_only_that_the_last_run_failed()
    {
        var system = new SystemTestHarness();
        system.Server.ServiceView = RouteAnswer.Api("services", "ran-failed");

        await system.Show();

        Assert.Equal("Last run failed.", Named(system, "heartbeat").Why);
    }

    [Fact]
    public async Task A_running_service_that_was_never_paused_or_failed_has_no_why()
    {
        var system = Harness(Row("heartbeat", "\"paused\":false"));

        await system.Show();

        Assert.Equal(string.Empty, Named(system, "heartbeat").Why);
    }

    private static string Clock(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    private static SystemTestHarness Harness(string row)
    {
        var system = new SystemTestHarness();
        system.Server.ServiceView = new RouteAnswer(HttpStatusCode.OK,
            $$$"""{"services":[{{{row}}}],"empty":null,"asOf":{"cursor":7,"ledgerHead":7,"projectedAt":"2026-01-01T00:00:00.0000000Z","rebuilding":false,"stalled":false}}""");
        return system;
    }

    private static string Row(string name, string extra)
    {
        var paused = extra.Contains("\"paused\"", StringComparison.Ordinal) ? string.Empty : "\"paused\":false,";
        var lastRun = extra.Contains("\"lastRun\"", StringComparison.Ordinal) ? string.Empty : "\"lastRun\":null,";
        return "{\"name\":\"" + name + "\",\"every\":\"5s\"," + paused + lastRun + "\"nextSlot\":null,\"job\":null,\"actions\":[\"run\",\"pause\"]," + extra + "}";
    }

    private static ViewModels.Shell.ServiceRowViewModel Named(SystemTestHarness system, string name) =>
        system.Vm.ServiceTable.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal));
}
