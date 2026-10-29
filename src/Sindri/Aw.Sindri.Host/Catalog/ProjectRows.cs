using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Features.HideProject;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The Projects page's writes (projects page spec §5.3): attach, change, remove, and hide or unhide one. Desk class like the item writes, so a session is refused; the page itself comes later, hence 'pending'.</summary>
internal static class ProjectRows
{
    private const string Ui = "pending:Projects";

    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("AttachProject", typeof(AttachProjectRequest), "/sindri/projects", "sindri project add [--linear <key>] [--name <text>] [--link <url>] [--state <state>] [--note <text>] [--target <date>]", destructive: false),
        Row("ChangeProject", typeof(ChangeProjectRequest), "/sindri/projects/{projectId}/change", "sindri project edit <project> --version <n> [--name <text>] [--link <url>] [--state <state>] [--clear-state] [--note <text>] [--target <date>]", destructive: false),
        Row("DetachProject", typeof(DetachProjectRequest), "/sindri/projects/{projectId}/detach", "sindri project remove <project> --version <n>", destructive: true),
        Row("HideProject", typeof(HideProjectRequest), "/sindri/projects/hide", "sindri project hide <project> --version <n>", destructive: false),
    ];

    private static CommandRow Row(string action, Type request, string route, string cli, bool destructive) =>
        new(ActionId.From(action), request, Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, destructive, ReadsViews: true, Shortcut: null,
            "POST", route, ShellRows.Form(cli), Ui);
}
