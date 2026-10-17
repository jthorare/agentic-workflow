using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Features.SessionHeartbeat;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// API P10a's session rows (API spec §8.1, §4.1): 'session wrap''s heartbeat and exit calls. Issues proven by the launch's own proof and never run by a person,
/// so the CLI form is the wrap that makes them.
/// </summary>
internal static class SessionRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Issue("SessionHeartbeat", typeof(SessionHeartbeatRequest), "/sindri/sessions/{launchId}/heartbeat"),
        Issue("SessionExit", typeof(SessionExitRequest), "/sindri/sessions/{launchId}/exit"),
    ];

    private static CommandRow Issue(string action, Type type, string route) =>
        new(ActionId.From(action), type, Variant: null, ActionClass.Issue, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", route, ShellRows.Form("sindri session wrap --launch <launchId>"), "ready");
}
