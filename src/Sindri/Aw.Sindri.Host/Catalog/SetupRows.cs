using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddRepo;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The setup-once rows (setup-once spec §7.4) that no unit has shipped yet (AddRepo, shipped by SO-A3, is ready), so the app and doctor can list them: UI status 'pending:&lt;unit&gt;'. The unit that builds each one gives it its type and flips it to 'ready'.
/// All are desk rows for a human; the automatic registration is the setup service's through ServiceGrants. The protected rows (ApproveProfile, ApproveRecipe, ApproveAllRecipes) are in their own lanes' rows.
/// </summary>
internal static class SetupRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        JobRows.Job("AddRepo", typeof(AddRepoRequest), "/sindri/setup/repos", "sindri repo add <path>"),   // a job-class row: a session is refused (SND-AUTH-002), the app and the terminal start it
        Pending("RegisterMcp", "/sindri/setup/mcp", "sindri mcp register --apply", "SO-A4") with { ServiceGrants = ["setup"] },
        Pending("SetupOnyx", "/sindri/onyx/setup", "sindri onyx up", "SO-A5"),
        Pending("ConnectOnyx", "/sindri/onyx/account", "sindri onyx connect", "SO-A5"),
        Pending("DeferOnyx", "/sindri/onyx/defer", "sindri onyx defer", "SO-A5"),
    ];

    private static CommandRow Pending(string action, string route, string cli, string unit) =>
        new(ActionId.From(action), CommandType: null, Variant: null, ActionClass.Desk, ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false,
            Shortcut: null, "POST", route, ShellRows.Form(cli), "pending:" + unit);
}
