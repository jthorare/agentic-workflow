using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.OpenItem;

namespace Aw.Sindri.Host.Catalog;

/// <summary>API P3's rows (plan G16): creating an item, and closing one as done or cancelled (API spec §3.2, UI spec E-8); API P10a's: opening one (UI spec E-1).</summary>
internal static class ItemRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("CreateItem"), typeof(CreateItemRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/items", ShellRows.Form("sindri item add <title> [--repo <repo>]"), "ready"),
        new(ActionId.From("CloseItem:done"), typeof(CloseItemRequest), "done", ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/items/{itemId}/close", ShellRows.Form("sindri item close <itemId> [--version <n>]"), "ready"),
        new(ActionId.From("CloseItem:cancelled"), typeof(CloseItemRequest), "cancelled", ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/items/{itemId}/close", ShellRows.Form("sindri item close <itemId> --cancelled [--version <n>]"), "ready"),
        Open("open", "sindri open <itemId> [--agent <agent>]"),
        Open("attach", "sindri open <itemId> --attach [--agent <agent>]"),
        Open("resume", "sindri open <itemId> --resume [--agent <agent>]"),
    ];

    /// <summary>An 'OpenItem' variant (API P10a, UI spec E-1): one POST route, desk, waits for the views.</summary>
    private static CommandRow Open(string variant, string cli) =>
        new(ActionId.From("OpenItem:" + variant), typeof(OpenItemRequest), variant, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/items/{itemId}/open", ShellRows.Form(cli), "ready");
}
