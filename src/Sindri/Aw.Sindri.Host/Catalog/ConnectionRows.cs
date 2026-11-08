using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// Connection writes (connections PR 10a): add one (a browser sign-in or a key), test it, remove it and its secret. Desk class, and an agent session may run all
/// three (owner decision D2), the way the MCP connection_* tools do. The reads (list, show) are queries and have no row. The terminal forms hold no secret: a key or a
/// client secret is named by the variable that holds it (--key-env, --client-secret-env), which the CLI reads from its own shell.
/// </summary>
internal static class ConnectionRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("AddConnection"), typeof(AddConnectionRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/connections", ShellRows.Form("sindri connection add <name> [--preset <preset>] [--kind <kind>] [--client-id <id>] [--client-secret-env <name>] [--key-env <name>]"), "ready", SessionOk: true),
        new(ActionId.From("TestConnection"), typeof(TestConnectionRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/connections/{name}/test", ShellRows.Form("sindri connection test <name>"), "ready", SessionOk: true),
        new(ActionId.From("RemoveConnection"), typeof(RemoveConnectionRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: false, Shortcut: null,
            "DELETE", "/sindri/connections/{name}", ShellRows.Form("sindri connection remove <name>"), "ready", SessionOk: true),
    ];
}
