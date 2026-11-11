using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.BuildToolkit;
using Aw.Sindri.Features.CreateToolkit;
using Aw.Sindri.Features.PauseToolkit;
using Aw.Sindri.Features.ResumeToolkit;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Features.StopToolkit;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// Toolkits (agent-first PR 16, 16c): create and build are jobs (a generator run and a build), start, stop, pause and resume are desk commands. An agent session may run all of them (SessionOk), the way
/// the toolkit_* MCP tools and 'sindri toolkit …' do: the build holds a heavy slot, a start refuses an unbuilt or changed toolkit (SND-TK-001), and the sandbox contains the process. The reads (list, calls)
/// are queries and have no row.
/// </summary>
internal static class ToolkitRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        JobRows.Job("CreateToolkit", typeof(CreateToolkitRequest), "/sindri/toolkits", "sindri toolkit create <name> --tool <tool>") with { SessionOk = true },
        JobRows.Job("BuildToolkit", typeof(BuildToolkitRequest), "/sindri/toolkits/build", "sindri toolkit build <name>") with { SessionOk = true },
        Desk("StartToolkit", typeof(StartToolkitRequest), "start"),
        Desk("StopToolkit", typeof(StopToolkitRequest), "stop"),
        Desk("PauseToolkit", typeof(PauseToolkitRequest), "pause"),
        Desk("ResumeToolkit", typeof(ResumeToolkitRequest), "resume"),
    ];

    private static CommandRow Desk(string action, Type request, string verb) =>
        new(ActionId.From(action), request, Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", $"/sindri/toolkits/{{name}}/{verb}", ShellRows.Form($"sindri toolkit {verb} <name>"), "ready", SessionOk: true);
}
