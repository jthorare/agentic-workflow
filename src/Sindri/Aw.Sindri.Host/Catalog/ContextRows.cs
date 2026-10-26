using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CodeIndexJobs;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The context engine's command rows (API spec §3.7; context spec §8.5, §8.7). Its GETs are queries and have no rows.
/// README X63 set the classes of the map review; the owner's q93 decision (2026-10-23) removed the typed challenge from the protected ones, so every review
/// action (a single decision, the bulk accept, undo, repoint, drop, apply-file, merge, add, delete) and promote are desk rows a session may also run;
/// import is a protected row with no typed confirmation, like every row but the holdout (group F). A session runs every job a human starts.
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
        .. Review("apply-file", "sindri context map review --apply <file>"),
        .. Review("merge", "sindri context map review merge <node> --into <node>"),
        .. Review("add", "sindri context map review add <node> <name> [--under <node>]"),
        .. Review("delete", "sindri context map review delete <node>"),
        new(ActionId.From("PromoteContextMap"), typeof(PromoteContextMapRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/promote", ShellRows.Form("sindri context map promote"), "ready", SessionOk: true),
        new(ActionId.From("ProposeContextMap"), typeof(ProposeContextMapRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/proposals", ShellRows.Form("sindri context map propose <file>"), "ready"),
        JobRows.Job("BuildContextMap", typeof(BuildContextMapRequest), "/sindri/context/map/build", "sindri context map build") with { SessionOk = true },
        JobRows.Job("RefreshContextMap", typeof(RefreshContextMapRequest), "/sindri/context/map/refresh", "sindri context map refresh") with { SessionOk = true },
        // PublishContextMap has no route and no verb of its own (code index spec 6.4): the context-map-refresh service starts it as itself, so the row names the verb that runs that service.
        new(ActionId.From("PublishContextMap"), typeof(PublishContextMapRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, ShellRows.Form("sindri services run context-map-refresh"), "ready"),
        // ScanContextConcepts has no route and no verb of its own (q110 C): the context-concept-scan service starts it as itself, so the row names the verb that runs that service.
        new(ActionId.From("ScanContextConcepts"), typeof(ScanContextConceptsRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, ShellRows.Form("sindri services run context-concept-scan"), "ready"),
        // BuildCodeIndex has no route and no verb of its own (code index spec 8): the code-index-main-move service starts it as itself, so the row names the verb that runs that service.
        new(ActionId.From("BuildCodeIndex"), typeof(BuildCodeIndexRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, ShellRows.Form("sindri services run code-index-main-move"), "ready"),
        // EmbedCodeIndex has no route and no verb of its own (code index spec 6.3): the code-index-embed service starts it as itself, so the row names the verb that runs that service.
        new(ActionId.From("EmbedCodeIndex"), typeof(EmbedCodeIndexRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, ShellRows.Form("sindri services run code-index-embed"), "ready"),
        new(ActionId.From("ImportContextMap"), typeof(ImportContextMapRequest), Variant: null, ActionClass.Protected, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/import", ShellRows.Form("sindri context map import <file>"), "ready"),
        JobRows.Job("SyncSources", typeof(SyncContextSourcesRequest), "/sindri/context/sources/sync", "sindri context sources sync"),
        JobRows.Job(BacktestAction, typeof(RunContextBacktestRequest), "/sindri/context/eval/backtest", "sindri context eval backtest") with { SessionOk = true },
    ];

    /// <summary>The backtest's action id (a shared contract with the UI lane: Verb+Context+noun); one constant, so a rename is one line.</summary>
    internal const string BacktestAction = "RunContextBacktest";

    private static CommandRow[] Review(string verb, string cli) =>
    [
        new(ActionId.From("ReviewContextMap:" + verb), typeof(ReviewContextMapRequest), verb, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/context/map/review", ShellRows.Form(cli), "ready", SessionOk: true),
    ];
}
