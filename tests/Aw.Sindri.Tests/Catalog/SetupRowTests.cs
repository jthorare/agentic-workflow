using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The setup-once catalog rows (spec §7.4): listed with their class, route and CLI form while the units that serve them are pending (AddRepo and RegisterMcp are ready); ApproveAllRecipes and the profile rows are already there.</summary>
public sealed class SetupRowTests
{
    [Fact, Trait("Lane", "unit")]
    public void AddRepo_is_a_ready_job_row_the_app_terminal_and_a_session_start_it()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "AddRepo");

        Assert.Equal((ActionClass.Job, "POST", "/sindri/setup/repos", "sindri repo add <path>", "ready", false, false), (row.Class, row.Method, row.Route, row.Cli.Text, row.Ui, row.NeedsTerminal, row.Destructive));
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Session().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().AtTerminal().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().Build(), row).Refusal);
    }

    [Fact, Trait("Lane", "unit")]
    public void OnboardRepo_is_a_ready_job_row_the_terminal_app_and_a_session_start()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "OnboardRepo");

        Assert.Equal((ActionClass.Job, "POST", "/sindri/setup/repos/onboard", "sindri repo onboard [<path>]", "ready", false, false), (row.Class, row.Method, row.Route, row.Cli.Text, row.Ui, row.NeedsTerminal, row.Destructive));
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Session().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().AtTerminal().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().Build(), row).Refusal);
    }

    [Fact, Trait("Lane", "unit")]
    public void InitProfile_is_a_cli_only_local_row()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "InitProfile");

        Assert.Equal((ActionClass.Local, "sindri profile init", "cli-only:local"), (row.Class, row.Cli.Text, row.Ui));
        Assert.Null(row.Route);
    }

    [Fact, Trait("Lane", "unit")]
    public void ConnectIndexSource_is_a_cli_only_local_row_held_to_a_terminal_with_no_route()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "ConnectIndexSource");

        Assert.Equal((ActionClass.Local, "sindri index connect <source>", "cli-only:local", true), (row.Class, row.Cli.Text, row.Ui, row.NeedsTerminal));
        Assert.Null(row.Route);
        Assert.Null(row.Method);
        Assert.Null(row.CommandType);
    }

    [Fact, Trait("Lane", "unit")]
    public void RegisterMcp_is_a_ready_job_row_the_app_and_a_session_start()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "RegisterMcp");

        Assert.Equal((ActionClass.Job, "POST", "/sindri/setup/mcp", "sindri mcp register --apply", "ready"), (row.Class, row.Method, row.Route, row.Cli.Text, row.Ui));
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Session().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().Build(), row).Refusal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_automatic_mcp_runs_are_the_setup_services_to_start()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "RegisterMcp");

        Assert.Equal(["setup"], row.ServiceGrants);
    }
}
