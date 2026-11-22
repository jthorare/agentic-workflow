using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Services card shows one count that agrees with its rows, and a profile that won't load gets one line saying why (#3623).</summary>
[Lane(TestLane.Unit)]
public sealed class ServicesOneCountTests
{
    private static ProfileView Invalid => new(Live: null, Problem: "line 3: unknown key", Source: null, ProblemKind: "invalid");

    [Fact]
    public async Task The_card_lists_its_rows_and_says_why_when_the_profile_wont_load()
    {
        var system = new SystemTestHarness();

        await system.Show(A.Status(profile: Invalid));

        Assert.Equal(("17 scheduled", 17, true), (system.Vm.ServicesScheduled, system.Vm.ServiceCard.Count, system.Vm.HasServices));
        Assert.Equal(Labels.ServicesProfileWontLoadKeepsLast, system.Vm.ServicesNote);   // the fixture's 17 rows hold profile services
    }

    [Fact]
    public async Task None_scheduled_is_for_an_empty_list_only_and_names_no_profile_reason()
    {
        var system = new SystemTestHarness();

        await system.Show(A.Status() with { Services = [] });   // the status counts none, the services read lists 17

        Assert.True(system.Vm.HasServices);
        Assert.Equal(string.Empty, system.Vm.ServicesNote);
        Assert.Equal("none scheduled", Labels.NoServices);
    }

    [Fact]
    public async Task A_still_run_profile_service_gets_a_note_that_does_not_say_only_built_ins_run()
    {
        var system = Harness(Row("heartbeat", "run", "pause"), Row("setup", "run"));

        await system.Show(A.Status(profile: Invalid));

        Assert.DoesNotContain("only the built-in", system.Vm.ServicesNote, StringComparison.Ordinal);
        Assert.Equal(Labels.ServicesProfileWontLoadKeepsLast, system.Vm.ServicesNote);
    }

    [Fact]
    public async Task A_list_of_built_ins_only_keeps_the_built_ins_only_note()
    {
        var system = Harness(Row("setup", "run"), Row("index", "run"));

        await system.Show(A.Status(profile: Invalid));

        Assert.Equal(Labels.ServicesProfileWontLoad, system.Vm.ServicesNote);
    }

    private static SystemTestHarness Harness(params string[] rows)
    {
        var system = new SystemTestHarness();
        system.Server.ServiceView = new RouteAnswer(HttpStatusCode.OK,
            $$$"""{"services":[{{{string.Join(',', rows)}}}],"empty":null,"asOf":{"cursor":7,"ledgerHead":7,"projectedAt":"2026-01-01T00:00:00.0000000Z","rebuilding":false,"stalled":false}}""");
        return system;
    }

    // A built-in allows run only; a profile service (including one still run from the last loaded profile) also allows pause or resume.
    private static string Row(string name, params string[] actions) =>
        "{\"name\":\"" + name + "\",\"every\":\"5s\",\"paused\":false,\"lastRun\":null,\"nextSlot\":null,\"job\":null,\"actions\":[" + string.Join(',', actions.Select(a => "\"" + a + "\"")) + "]}";
}
