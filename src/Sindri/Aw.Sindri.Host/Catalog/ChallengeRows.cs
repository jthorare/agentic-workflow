using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.IssueChallenge;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The challenge rows (engine spec §4.2, setup-once spec §4 item 1): the terminal's challenge for a protected action and the
/// one the Sindri app confirms. Only 'approve profile' still asks for one.
/// </summary>
internal static class ChallengeRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("IssueAppChallenge", typeof(IssueAppChallengeRequest), ActionClass.Desk, "/sindri/workflow/challenges/app"),
        Row("IssueChallenge", typeof(IssueChallengeRequest), ActionClass.Issue, "/sindri/workflow/challenges"),
    ];

    private static CommandRow Row(string action, Type type, ActionClass actionClass, string route) =>
        new(ActionId.From(action), type, Variant: null, actionClass,
            ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false,
            Shortcut: null, "POST", route, ShellRows.Form("sindri profile approve <hash>"), "ready");
}
