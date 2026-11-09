using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Features.RegisterMcp;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The setup-once rows (setup-once spec §7.4) (AddRepo, OnboardRepo, RegisterMcp and the sources step's ConnectIndexSource), so the app and doctor can list them.
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
    ];
}
