using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>Each action of a row shows its own catalog command, its placeholders filled from the row's subject (the goldens decide every literal).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouActionCliTests
{
    private static readonly HomeCounts _counts = new(Running: 0, WithYou: 0, Queued: 0);

    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    private static CommandList Catalog() => Golden<CommandList>("commands", "catalog");

    private static string CatalogCli(string action) => Catalog().Rows.Single(r => r.Action == action).Cli;

    private static NeedsYouRowView Failure() =>
        Golden<NeedsYouList>("needs-you", "fixture").Rows.Single(r => r.Kind == NeedsYouKinds.Failure);

    private static NeedsYouEntry Map(NeedsYouRowView row, CommandList? catalog = null) =>
        NeedsYouMapper.Map(Golden<NeedsYouList>("needs-you", "fixture") with { Rows = [row] }, catalog ?? Catalog(), decisions: null, _counts).Rows[0];

    private static NeedsYouAction Pause(NeedsYouEntry entry) => entry.Actions.Single(a => a.CatalogId == "PauseService");

    [Fact]
    public void A_failure_rows_pause_shows_the_catalogs_pause_command_for_its_service()
    {
        var row = Failure();
        var service = row.Subject!.Id;

        var pause = Pause(Map(row));

        Assert.Equal(CatalogCli("PauseService").Replace("<name>", service, StringComparison.Ordinal), pause.Cli);
        Assert.Empty(pause.FillInHint);
    }

    [Fact]
    public void The_primary_action_keeps_the_rows_served_cli()
    {
        var row = Failure();

        var run = Map(row).Actions.Single(a => a.CatalogId == row.PrimaryAction);

        Assert.Equal(row.Cli, run.Cli);
    }

    [Fact]
    public void An_action_the_catalog_lacks_keeps_the_rows_cli()
    {
        var row = Failure();
        var catalog = Catalog();

        var pause = Pause(Map(row, catalog with { Rows = [.. catalog.Rows.Where(r => r.Action != "PauseService")] }));

        Assert.Equal(row.Cli, pause.Cli);
        Assert.Empty(pause.FillInHint);
    }

    [Fact]
    public void A_placeholder_the_subject_cannot_fill_is_shown_as_served()
    {
        var pause = Pause(Map(Failure() with { Subject = null }));

        Assert.Equal(CatalogCli("PauseService"), pause.Cli);
        Assert.Equal(Labels.FillInHint(["name"]), pause.FillInHint);
    }
}
