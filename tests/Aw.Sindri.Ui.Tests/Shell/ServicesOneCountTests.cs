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
        Assert.Equal(Labels.ServicesProfileWontLoad, system.Vm.ServicesNote);
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
}
