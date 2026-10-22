using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

[Lane(TestLane.Unit)]
public sealed class HealthCardsTests
{
    private static readonly string[] _wanted = ["pid · epoch", "API", "approved", "file"];

    private static string[] Flat(IEnumerable<HealthCell> cells) => [.. cells.SelectMany(c => new[] { c.Label, c.Value })];

    [Fact]
    public async Task The_control_plane_and_profile_cards_carry_the_rows_the_expander_held()
    {
        var system = new SystemTestHarness();

        await system.Show();

        Assert.Equal(["pid · epoch", "API"], system.Vm.Detail.Process.Select(c => c.Label).Where(l => _wanted.Contains(l, StringComparer.Ordinal)), StringComparer.Ordinal);
        Assert.Equal(["approved", "file"], system.Vm.Detail.Profile.Select(c => c.Label).Where(l => _wanted.Contains(l, StringComparer.Ordinal)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_views_card_is_up_to_date_with_how_far_it_has_read()
    {
        var system = new SystemTestHarness();

        await system.Show();

        Assert.Equal(Labels.ViewsUpToDate, system.Vm.ViewsWord);
        Assert.Equal([Labels.ViewsReadUpTo, "event 5 of 5", Labels.ViewsUpdated], Flat(system.Vm.ViewsRows).Take(3), StringComparer.Ordinal);
        Assert.Null(system.Vm.ViewsFix);
    }

    [Fact]
    public async Task Rebuilding_views_say_so_and_offer_the_rebuild_control()
    {
        var system = new SystemTestHarness("rebuilding");

        await system.Show();

        Assert.Equal(Labels.ViewsCardRebuilding, system.Vm.ViewsWord);
        Assert.Equal("RebuildViews", system.Vm.ViewsFix!.Action);
    }

    [Fact]
    public async Task Stalled_views_say_so_and_offer_the_rebuild_control()
    {
        var system = new SystemTestHarness("stalled");

        await system.Show();

        Assert.Equal(Labels.ViewsStalled, system.Vm.ViewsWord);
        Assert.Equal("RebuildViews", system.Vm.ViewsFix!.Action);
    }

    [Fact]
    public async Task The_agents_card_counts_running_asking_and_failed_sources()
    {
        var system = new SystemTestHarness();

        await system.Show(A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0) });

        Assert.Equal([Labels.AgentsRunning, "3", Labels.AgentsAsking, "1", Labels.AgentsProblems, "0"], Flat(system.Vm.AgentsRows), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_actions_card_says_in_numbers_what_the_app_does()
    {
        var system = new SystemTestHarness();

        await system.Show();

        var catalog = system.Vm.Catalog;
        Assert.Equal(Labels.ActionCounts(catalog.Controls.Count(c => c.IsReady), catalog.CliOnly.Count, catalog.Controls.Count(c => c.IsPending)), catalog.Counts);
    }
}
