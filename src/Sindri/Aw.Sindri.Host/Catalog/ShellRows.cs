using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The shipped rows: the app shell's (S1) two commands and two CLI-local verbs, P10a's launch init and show (CLI-local), and P2's RebuildViews (API spec §3.2, §3.1; UI spec §7).</summary>
internal static class ShellRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("StopProcess"), typeof(StopProcessRequest), Variant: null, ActionClass.Desk, ServiceGrants: ["host"],
            NeedsTerminal: false, SelfAudited: true, Destructive: true, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/stop", Form("sindri stop"), "ready"),
        new(ActionId.From("ApproveProfile"), typeof(ApproveProfileRequest), Variant: null, ActionClass.Protected, ServiceGrants: [],
            NeedsTerminal: true, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/profile/approve", Form("sindri profile approve [<hash>]"), "cli-only:typed-confirmation"),
        new(ActionId.From("RebuildViews"), typeof(RebuildViewsRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/views/rebuild", Form("sindri views rebuild"), "ready"),
        new(ActionId.From("RunDoctor"), CommandType: null, Variant: null, ActionClass.Local, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, Form("sindri doctor"), "cli-only:local"),
        new(ActionId.From("ValidateProfile"), CommandType: null, Variant: null, ActionClass.Local, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, Form("sindri profile validate"), "cli-only:local"),
        new(ActionId.From("LaunchInit"), CommandType: null, Variant: null, ActionClass.Local, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, Form("sindri launch init [--viewer <name>] [--force] [--stdout]"), "cli-only:local"),
        new(ActionId.From("LaunchShow"), CommandType: null, Variant: null, ActionClass.Local, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, Form("sindri launch show [<id>] [--json]"), "cli-only:local"),
    ];

    /// <summary>A checked CLI form; a row with a malformed one is a bug, so it throws at start-up (and in the catalog tests).</summary>
    internal static CliForm Form(string text) => CliForm.Parse(text).Value;
}
