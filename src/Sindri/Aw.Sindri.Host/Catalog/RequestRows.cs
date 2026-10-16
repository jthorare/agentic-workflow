using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Features.FileRequest;

namespace Aw.Sindri.Host.Catalog;

/// <summary>API P4's rows: filing a request (a session or a human) and deciding one (a human only; API spec §3.2, §4.1, UI spec E-8). Both wait for the views (D2). 'Decide' has no shortcut: the X key reads the option's rejectLike (G10).</summary>
internal static class RequestRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("FileRequest"), typeof(FileRequestRequest), Variant: null, ActionClass.Request, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/requests", ShellRows.Form("sindri request <kind> [--item <itemId>] <text>"), "ready"),
        new(ActionId.From("Decide"), typeof(DecideRequest), Variant: null, ActionClass.Decision, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/decisions/{decisionId}", ShellRows.Form("sindri decide <decisionId> <choice> [--note <note>]"), "ready"),
    ];
}
