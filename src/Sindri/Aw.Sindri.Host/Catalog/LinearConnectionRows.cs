using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Host.Catalog;

/// <summary>Settings, Connections, Linear's writes (Linear hub spec 2026-11-04 §3). Desk class like the item writes; the services themselves refuse a session, because the connection is the owner's.</summary>
internal static class LinearConnectionRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("ListLinearTeams"), typeof(ListLinearTeamsRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/linear/connection/teams", ShellRows.Form("sindri index connect linear"), "ready"),
        new(ActionId.From("SaveLinearConnection"), typeof(SaveLinearConnectionRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/linear/connection", ShellRows.Form("sindri index connect linear"), "ready"),
        new(ActionId.From("RemoveLinearWriteKey"), typeof(RemoveLinearWriteKeyRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: false, Shortcut: null,
            "DELETE", "/sindri/linear/connection/write", ShellRows.Form("sindri profile set context.sources.linear.writeToken <pointer>"), "ready"),
    ];
}
