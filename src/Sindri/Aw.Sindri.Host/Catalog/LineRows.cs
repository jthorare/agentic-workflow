using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.LineHoldout;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The line's command rows (line spec LN-API5). Reading a phase's hidden scenarios is protected, like import: the terminal and the typed confirmation, never an agent session, because the text
/// is what the builder must not see. 'line holdout add' is not here yet: it re-runs the author Step, which waits on the engine.
/// </summary>
internal static class LineRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("ShowLineHoldout"), typeof(ShowLineHoldoutRequest), Variant: null, ActionClass.Protected, ServiceGrants: [],
            NeedsTerminal: true, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/line/holdout/show", ShellRows.Form("sindri line holdout show <line> <phase>"), "cli-only:typed-confirmation"),
    ];
}
