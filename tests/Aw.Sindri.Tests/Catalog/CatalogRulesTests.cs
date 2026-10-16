using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The command catalog's unit rules (API spec §3.3, UI spec E-8, E-16, §8, P1 plan G16), on the Host's catalog and on fixtures.</summary>
public sealed class CatalogRulesTests
{
    private static IReadOnlyList<CommandRow> Shipped => new CommandCatalog().Rows;

    [Fact, Trait("Lane", "unit")]
    public void Every_catalog_cli_is_built_from_checked_parts() =>
        Assert.Empty(CatalogRules.UncheckedCli(Shipped));

    [Fact, Trait("Lane", "unit")]
    public void Catalog_shortcuts_avoid_the_reserved_keys()
    {
        Assert.Empty(CatalogRules.ReservedShortcuts(Shipped));
        Assert.Equal(4, CatalogRules.ReservedShortcuts([Keyed("Shift+A"), Keyed("y"), Keyed("/"), Keyed("?")]).Count());
        Assert.Empty(CatalogRules.ReservedShortcuts([Keyed("X"), Keyed("Shift+X"), Keyed(shortcut: null)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Cli_only_rows_are_terminal_bound()
    {
        Assert.Empty(CatalogRules.LooseCliOnly(Shipped));
        _ = Assert.Single(CatalogRules.LooseCliOnly([Row(ActionClass.Desk, "cli-only:typed-confirmation")]));
        Assert.Empty(CatalogRules.LooseCliOnly([Row(ActionClass.Local, "cli-only:local"), Row(ActionClass.Desk, "cli-only:typed-confirmation", needsTerminal: true)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_pending_row_names_a_shipped_phase()
    {
        Assert.Empty(CatalogRules.ShippedPending(Shipped));
        Assert.Equal(3, CatalogRules.ShippedPending([Row(ActionClass.Desk, "pending:P1"), Row(ActionClass.Desk, "pending:S1"), Row(ActionClass.Desk, "pending:P4")]).Count());
        Assert.Empty(CatalogRules.ShippedPending([Row(ActionClass.Desk, "pending:P6"), Row(ActionClass.Desk, "pending:P10a")]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_ui_status_is_ready_pending_or_cli_only()
    {
        Assert.Empty(CatalogRules.UnknownUi(Shipped));
        Assert.Equal(4, CatalogRules.UnknownUi([Row(ActionClass.Desk, "Ready"), Row(ActionClass.Desk, "pending:"), Row(ActionClass.Desk, "cli-only:"), Row(ActionClass.Desk, "soon")]).Count());
    }

    [Fact, Trait("Lane", "unit")]
    public void Stop_is_destructive_and_no_shell_row_reads_views()
    {
        Assert.True(Assert.Single(ShellRows.All, row => row.Action.Value == "StopProcess").Destructive);
        Assert.All(ShellRows.All, row => Assert.False(row.ReadsViews));
    }

    private static CommandRow Keyed(string? shortcut) => Row(ActionClass.Desk, "ready") with { Shortcut = shortcut };

    private static CommandRow Row(ActionClass actionClass, string ui, bool needsTerminal = false) =>
        new(ActionId.From("FixtureRow"), CommandType: null, Variant: null, actionClass, ServiceGrants: [], needsTerminal, SelfAudited: false,
            Destructive: false, ReadsViews: false, Shortcut: null, Method: null, Route: null, CliForm.Parse("sindri fixture").Value, ui);
}
