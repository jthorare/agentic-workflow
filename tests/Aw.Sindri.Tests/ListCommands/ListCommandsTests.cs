using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ListCommands;

/// <summary>GET /sindri/commands (API spec §3.1, UI spec E-8): the catalog as the app and doctor read it.</summary>
public sealed class ListCommandsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListCommands_lists_every_action_id_with_its_class()
    {
        var catalog = new CommandCatalog();
        var service = new ListCommandsService(catalog);

        var result = await service.Handle(A.ListCommandsRequest().Build(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            catalog.Rows.OrderBy(row => row.Action.Value, StringComparer.Ordinal).Select(row => (row.Action.Value, row.Class)),
            result.Value.Rows.Select(row => (row.Action.Value, row.Class)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Allowed_callers_follow_the_authorization_table()
    {
        var service = new ListCommandsService(new InMemoryCommandCatalog(
            Row("StopProcess", ActionClass.Desk, grants: ["host"]),
            Row("ApproveProfile", ActionClass.Desk, needsTerminal: true),
            Row("RunDoctor", ActionClass.Local),
            Row("FileRequest", ActionClass.Request),
            Row("RunService", ActionClass.Job),
            Row("PauseService", ActionClass.Pause),
            Row("Decide", ActionClass.Decision),
            Row("ReportStep", ActionClass.StepReport),
            Row("IssueNonce", ActionClass.Issue),
            Row("WipeItem", ActionClass.Desk, needsTerminal: true, grants: ["host"])));

        var rows = (await service.Handle(new ListCommandsRequest(), TestContext.Current.CancellationToken)).Value.Rows
            .ToDictionary(row => row.Action.Value, StringComparer.Ordinal);

        Assert.Equal(["human+terminal", "human", "session", "service:host"], rows["StopProcess"].AllowedCallers);
        Assert.Equal(["human+terminal", "human", "session"], rows["ApproveProfile"].AllowedCallers);
        Assert.Empty(rows["RunDoctor"].AllowedCallers);
        Assert.Equal(["human+terminal", "human", "session", "service"], rows["FileRequest"].AllowedCallers);
        Assert.Equal(["human+terminal", "human", "session", "service"], rows["RunService"].AllowedCallers);
        Assert.Equal(["human+terminal", "human", "session", "service"], rows["PauseService"].AllowedCallers);
        Assert.Equal(["human+terminal", "human", "session"], rows["Decide"].AllowedCallers);
        Assert.Equal(["session"], rows["ReportStep"].AllowedCallers);
        Assert.Equal(["human+terminal", "human", "session"], rows["IssueNonce"].AllowedCallers);
        Assert.Equal(["human+terminal", "human", "session", "service:host"], rows["WipeItem"].AllowedCallers);   // the terminal flag no longer narrows
        Assert.True(rows["WipeItem"].Terminal);
        Assert.False(rows["StopProcess"].Terminal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_row_carries_its_route_cli_ui_status_and_hints()
    {
        var row = Row("CloseItem:cancelled", ActionClass.Desk) with
        {
            Destructive = true,
            ReadsViews = true,
            Shortcut = "Shift+X",
            Method = "POST",
            Route = "/sindri/items/{itemId}/close",
            Cli = CliForm.Parse("sindri item close <itemId> --cancelled").Value,
            Ui = "pending:P3",
        };
        var service = new ListCommandsService(new InMemoryCommandCatalog(row));

        var listed = Assert.Single((await service.Handle(new ListCommandsRequest(), TestContext.Current.CancellationToken)).Value.Rows);

        Assert.Equal(
            new CommandListRow(ActionId.From("CloseItem:cancelled"), ActionClass.Desk, listed.AllowedCallers, Terminal: false, "POST",
                "/sindri/items/{itemId}/close", "sindri item close <itemId> --cancelled", "pending:P3", Destructive: true, ReadsViews: true, "Shift+X"),
            listed);
    }

    private static CommandRow Row(string action, ActionClass actionClass, bool needsTerminal = false, string[]? grants = null) =>
        new(ActionId.From(action), CommandType: null, action.Contains(':', StringComparison.Ordinal) ? action[(action.IndexOf(':', StringComparison.Ordinal) + 1)..] : null,
            actionClass, grants ?? [], needsTerminal, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, CliForm.Parse("sindri fixture").Value, "ready");
}
