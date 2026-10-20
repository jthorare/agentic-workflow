using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The context engine's command rows (API spec §3.7; context spec §8.5, §8.7). Its GETs are queries and have no rows.
/// README X63 sets the classes of the map review: a single decision (accept, reject, rename, move), the bulk deterministic accept,
/// undo and the orphan repoint and drop are desk (one reversible override row, with Undo; the terminal and the app may run them);
/// applying a review file, merge, add, delete and promote are protected (the terminal and the typed confirmation of X30, like import).
/// Proposing is a job run synchronously (it only writes draft rows). BuildContextMap, SyncSources and RunContextBacktest are jobs on API P6's runtime (JobRows.Job,
/// AddContextJobs, MapContextJobs): they start a job and answer its links, and the 'context-rebuild' and 'context-sync' scheduled services
/// run the same work on a cadence.
/// </summary>
internal static class ContextRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        .. Review("accept", "sindri context map review accept <target>"),
        .. Review("reject", "sindri context map review reject <target>"),
        .. Review("rename", "sindri context map review rename <node> <name>"),
        .. Review("move", "sindri context map review move <node> --under <node>"),
        .. Review("accept-deterministic", "sindri context map review --accept-deterministic"),
        .. Review("undo", "sindri context map review --undo <id>"),
        .. Review("drop", "sindri context map review --drop <id>"),
        .. Review("repoint", "sindri context map review --repoint <id> --to <node>"),
        .. Protected("apply-file", "sindri context map review --apply <file>"),
        .. Protected("merge", "sindri context map review merge <node> --into <node>"),
        .. Protected("add", "sindri context map review add <node> <name> [--under <node>]"),
        .. Protected("delete", "sindri context map review delete <node>"),
        new(ActionId.From("PromoteContextMap"), typeof(PromoteContextMapRequest), Variant: null, ActionClass.Protected, ServiceGrants: [],
            NeedsTerminal: true, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/promote", ShellRows.Form("sindri context map promote"), EngineRowsTypedConfirmation),
        new(ActionId.From("ProposeContextMap"), typeof(ProposeContextMapRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/proposals", ShellRows.Form("sindri context map propose <file>"), "ready"),
        JobRows.Job("BuildContextMap", typeof(BuildContextMapRequest), "/sindri/context/map/build", "sindri context map build"),
        JobRows.Job("RefreshContextMap", typeof(RefreshContextMapRequest), "/sindri/context/map/refresh", "sindri context map refresh"),
        new(ActionId.From("ImportContextMap"), typeof(ImportContextMapRequest), Variant: null, ActionClass.Protected, ServiceGrants: [],
            NeedsTerminal: true, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/import", ShellRows.Form("sindri context map import <file>"), "cli-only:typed-confirmation"),
        JobRows.Job("SyncSources", typeof(SyncContextSourcesRequest), "/sindri/context/sources/sync", "sindri context sources sync"),
        JobRows.Job(BacktestAction, typeof(RunContextBacktestRequest), "/sindri/context/eval/backtest", "sindri context eval backtest"),
    ];

    /// <summary>The backtest's action id (a shared contract with the UI lane: Verb+Context+noun); one constant, so a rename is one line.</summary>
    internal const string BacktestAction = "RunContextBacktest";

    private const string EngineRowsTypedConfirmation = "cli-only:typed-confirmation";

    private static CommandRow[] Review(string verb, string cli) =>
    [
        new(ActionId.From("ReviewContextMap:" + verb), typeof(ReviewContextMapRequest), verb, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/review", ShellRows.Form(cli), "ready"),
    ];

    private static CommandRow[] Protected(string verb, string cli) =>
    [
        new(ActionId.From("ReviewContextMap:" + verb), typeof(ReviewContextMapRequest), verb, ActionClass.Protected, ServiceGrants: [],
            NeedsTerminal: true, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/review", ShellRows.Form(cli), EngineRowsTypedConfirmation),
    ];
}
