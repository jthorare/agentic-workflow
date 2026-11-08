using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The System page's remaining pieces (system spec §2, §5, §6): the narrow table, View job, Left and Right between the tabs, and sentence-case captions.</summary>
[Lane(TestLane.Unit)]
public sealed class ServicesTabLayoutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Under_900_px_the_table_and_its_rows_go_compact_and_back()
    {
        var system = new SystemTestHarness();
        await system.Show();

        system.Vm.Resize(899);
        Assert.Equal((true, false), (system.Vm.IsCompact, system.Vm.IsWide));
        Assert.All(system.Vm.ServiceTable, r => Assert.True(r.IsCompact));

        system.Vm.Resize(900);
        Assert.Equal((false, true), (system.Vm.IsCompact, system.Vm.IsWide));
        Assert.All(system.Vm.ServiceTable, r => Assert.True(r.IsWide));
    }

    [Fact]
    public async Task A_row_that_arrives_after_the_page_narrowed_is_narrow_too()
    {
        var system = new SystemTestHarness();
        system.Vm.Resize(700);

        await system.Show();

        Assert.All(system.Vm.ServiceTable, r => Assert.True(r.IsCompact));
    }

    [Fact]
    public async Task View_job_asks_for_the_running_job_and_a_row_without_one_asks_for_nothing()
    {
        var system = new SystemTestHarness("running-job");
        await system.Show();
        var asked = new List<DeepLink>();
        system.Vm.JobRequested += (_, e) => asked.Add(e.Link);
        var stuck = system.Vm.ServiceTable.Single(r => string.Equals(r.Name, "stuck", StringComparison.Ordinal));

        stuck.RequestJob();
        system.Vm.ServiceTable.First(r => !r.IsRunning).RequestJob();

        Assert.True(stuck.IsRunning);
        Assert.Equal([new DeepLink(DeepLinkKind.Job, stuck.JobId)], asked);
    }

    [Fact]
    public Task View_job_in_the_shell_opens_the_Jobs_page_on_that_jobs_sheet() => UiThread.Run(async () =>
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var server = new FakeSindriApi
        {
            Catalog = RouteAnswer.Api("commands", "catalog"),
            NeedsYouView = RouteAnswer.Api("needs-you", "empty"),
            ServiceView = RouteAnswer.Api("services", "running-job"),
        };
        var shell = new ShellViewModel(records, server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.Health.ServiceTable.Count == 0; i++)
        {
            await Task.Delay(5, Token);
        }

        var stuck = shell.Health.ServiceTable.Single(r => string.Equals(r.Name, "stuck", StringComparison.Ordinal));
        stuck.RequestJob();

        Assert.True(shell.IsJobs);
        Assert.True(shell.IsJobSheet);
        Assert.Equal(stuck.JobId, shell.SheetLink?.Subject);
    }, Token);

    [Fact]
    public async Task Right_moves_to_Services_and_Left_back_and_neither_goes_past_an_end()
    {
        var system = new SystemTestHarness();
        await system.Show();

        Assert.False(system.Vm.MoveTab(-1));
        Assert.True(system.Vm.MoveTab(1));
        Assert.True(system.Vm.ShowServicesTab);
        Assert.False(system.Vm.MoveTab(1));
        Assert.True(system.Vm.MoveTab(-1));
        Assert.True(system.Vm.ShowOverview);
    }

    [Fact]
    public void Card_captions_are_sentence_case()
    {
        Assert.Equal(["Control plane", "Lease", "Profile", "Services", "Last event", "Actions"],
            [Labels.ControlPlaneCard, Labels.LeaseCard, Labels.ProfileCard, Labels.ServicesCard, Labels.LastEventCard, Labels.ActionsCard],
            StringComparer.Ordinal);
    }
}
