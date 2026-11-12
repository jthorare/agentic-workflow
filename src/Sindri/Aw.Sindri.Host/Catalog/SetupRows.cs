using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Features.SkipSetupStep;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The setup-once rows (setup-once spec §7.4) (AddRepo, OnboardRepo, RegisterMcp and the sources step's ConnectIndexSource), so the app and doctor can list them, and the first-run rows (first-run spec §3.1, §7):
/// SkipSetupStep (any caller); RestartProcess and ConnectAccount (Desk rows whose services refuse a caller without the per-start human token).
/// All are meant for a human at the app or a terminal, but an agent session may run them too; the automatic registration is the setup service's through ServiceGrants. The protected rows (ApproveProfile) are in their own lanes' rows.
/// </summary>
internal static class SetupRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        JobRows.Job("AddRepo", typeof(AddRepoRequest), "/sindri/setup/repos", "sindri repo add <path>"),   // a job-class row: any caller may start it (agent-first PR 2)
        JobRows.Job("OnboardRepo", typeof(OnboardRepoRequest), "/sindri/setup/repos/onboard", "sindri repo onboard [<path>]"),   // a job-class row like AddRepo: the terminal, the app and an agent session may start it; the setup service onboards on its own after an approval
        JobRows.Job("RegisterMcp", typeof(RegisterMcpJobRequest), "/sindri/setup/mcp", "sindri mcp register --apply") with { ServiceGrants = ["setup"] },
        // the sources step's action is `sindri index connect <source>`: local (no route); the token comes from a terminal prompt or a `--token-env NAME` pointer, so an agent session may run it too (agent-first PR 2)
        new(ActionId.From("ConnectIndexSource"), CommandType: null, Variant: null, ActionClass.Local, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, ShellRows.Form("sindri index connect <source>"), "cli-only:local"),
        // a skip is undone in one click, so any caller may run it (first-run spec §3.1)
        new(ActionId.From("SkipSetupStep"), typeof(SkipSetupStepRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/setup/skip", ShellRows.Form("sindri setup skip <step> [--undo]"), "ready"),
        // the owner's: the service checks the per-start human token, as `profile set` does (first-run spec decision 11). Pending until first-run phase 2 builds the restart: the service is a stub, so the app draws no button (FR2 is the first-run spec's phase, not a catalog phase)
        new(ActionId.From("RestartProcess"), typeof(RestartProcessRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/restart", ShellRows.Form("sindri restart [--wait]"), "pending:FR2"),
        // the owner's: signs in or takes a pasted token for github, linear or slack (first-run spec decisions 13 to 17). Pending until first-run phase 5 builds the accounts (FR5)
        new(ActionId.From("ConnectAccount"), typeof(ConnectAccountRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/setup/accounts/{provider}", ShellRows.Form("sindri setup connect <provider>"), "pending:FR5"),
    ];
}
