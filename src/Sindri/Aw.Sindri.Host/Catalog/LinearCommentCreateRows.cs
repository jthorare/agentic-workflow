using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Host.Catalog;

/// <summary>The Linear hub's comment and create writes (spec §5.2, §5.3). Desk class like the other owner writes; the service itself refuses a session, because Linear writes are the owner's.</summary>
internal static class LinearCommentCreateRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("CommentLinearIssue"), typeof(CommentLinearIssueRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/linear/issues/{key}/comments", ShellRows.Form("sindri linear comment <key> <text>"), "ready"),
        new(ActionId.From("CreateLinearIssue"), typeof(CreateLinearIssueRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/linear/issues", ShellRows.Form("sindri linear create <team> <title>"), "ready"),
    ];
}
