using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CompleteStep;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The completion rows (API spec §4.1): the human's desk row and a launched agent session's report, both on one route. The report is a 'run_agent' node's (PR 7): the route's
/// instance is the run id and its run is the node's token (RunAgentWire).
/// </summary>
internal static class CompleteStepRows
{
    private const string Route = "/sindri/workflow/instances/{instanceId}/runs/{run}/complete";

    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("CompleteStep", ActionClass.Desk),
        Row("CompleteStep:report", ActionClass.StepReport),
    ];

    private static CommandRow Row(string action, ActionClass actionClass)
    {
        var colon = action.IndexOf(':', StringComparison.Ordinal);
        return new(ActionId.From(action), typeof(CompleteStepRequest), colon < 0 ? null : action[(colon + 1)..], actionClass,
            ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false,
            Shortcut: null, "POST", Route, ShellRows.Form("sindri workflow complete <address> --run <token> --outcome <outcome>"), "ready");
    }
}
