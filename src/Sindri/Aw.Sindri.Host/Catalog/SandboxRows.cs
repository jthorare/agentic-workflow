using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AllowDenial;
using Aw.Sindri.Features.EndLearnMode;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The sandbox's rows (sandbox spec §3.6): ending network learn mode early, and allowing a blocked host or home path (a draft profile edit the owner approves).</summary>
internal static class SandboxRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("EndLearnMode"), typeof(EndLearnModeRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/sandbox/learn-off", ShellRows.Form("sindri sandbox learn off [--repo <repo>] [--json]"), "ready"),
        new(ActionId.From("AllowDenial"), typeof(AllowDenialRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/sandbox/allow", ShellRows.Form("sindri sandbox allow <launch> <target>"), "ready"),
    ];
}
