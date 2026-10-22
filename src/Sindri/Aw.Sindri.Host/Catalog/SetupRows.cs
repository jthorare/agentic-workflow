using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Features.ConnectOnyx;
using Aw.Sindri.Features.DeferOnyx;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.SetupOnyx;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The setup-once rows (setup-once spec §7.4) (AddRepo, RegisterMcp and the Onyx rows), so the app and doctor can list them.
/// All are desk rows for a human; the automatic registration is the setup service's through ServiceGrants. The protected rows (ApproveProfile, ApproveRecipe, ApproveAllRecipes) are in their own lanes' rows.
/// </summary>
internal static class SetupRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        JobRows.Job("AddRepo", typeof(AddRepoRequest), "/sindri/setup/repos", "sindri repo add <path>"),   // a job-class row: a session is refused (SND-AUTH-002), the app and the terminal start it
        JobRows.Job("OnboardRepo", typeof(OnboardRepoRequest), "/sindri/setup/repos/onboard", "sindri repo onboard [<path>]"),   // a job-class row like AddRepo: the terminal and the app start it, a session is refused; the setup service onboards on its own after an approval
        JobRows.Job("RegisterMcp", typeof(RegisterMcpJobRequest), "/sindri/setup/mcp", "sindri mcp register --apply") with { ServiceGrants = ["setup"] },
        JobRows.Job("SetupOnyx", typeof(SetupOnyxRequest), "/sindri/onyx/setup", "sindri onyx up"),
        Ready("ConnectOnyx", typeof(ConnectOnyxRequest), "/sindri/onyx/account", "sindri onyx connect"),
        Ready("DeferOnyx", typeof(DeferOnyxRequest), "/sindri/onyx/defer", "sindri onyx defer"),
    ];

    private static CommandRow Ready(string action, Type request, string route, string cli) =>
        new(ActionId.From(action), request, Variant: null, ActionClass.Desk, ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false,
            Shortcut: null, "POST", route, ShellRows.Form(cli), "ready");
}
