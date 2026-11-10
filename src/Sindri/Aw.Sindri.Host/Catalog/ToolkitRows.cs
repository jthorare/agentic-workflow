using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.BuildToolkit;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// Toolkits (agent-first PR 16): the build job. It has no route of its own: the toolkit_build MCP tool and the build verb start it, so the row names that verb. Start, stop and list are plain
/// requests without IJob, so they have no row.
/// </summary>
internal static class ToolkitRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("BuildToolkit"), typeof(BuildToolkitRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, ShellRows.Form("sindri toolkit build <name>"), "ready"),
    ];
}
