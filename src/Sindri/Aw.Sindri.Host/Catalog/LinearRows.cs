using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The Linear hub's writes (Linear hub spec 2026-11-04 §3, Ph3 1b). Desk class like the project writes, and a session is refused by the service (SND-AUTH-002): the issue sheet edits an issue.</summary>
internal static class LinearRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("ChangeLinearIssue", typeof(ChangeLinearIssueRequest), "/sindri/linear/issues/{key}/change", "sindri linear edit <issue> [--title <text>] [--state <state>] [--assignee <person>] [--priority <level>]"),
    ];

    private static CommandRow Row(string action, Type request, string route, string cli) =>
        new(ActionId.From(action), request, Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", route, ShellRows.Form(cli), "ready");
}
