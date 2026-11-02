using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.IssueNonce;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Features.StartInstance;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The workflow engine's rows (engine spec §6.2, §6.3 route table; UI spec §7, E-9, E-12; API spec §2.1, §4.1). P2's
/// recipe commands are bound to their types and flip to 'ready' as their units ship them; protected rows end as
/// 'ready' once mapped. P4's instance rows and P5's takeover and handback are bound and mapped.
/// Queries (show, list, get, events) have no rows: the catalog lists commands, variants and issues only (API spec §3.3).
/// </summary>
internal static class EngineRows
{
    /// <summary>The UI status a protected row takes once its endpoint is mapped.</summary>
    private const string Commands = "POST /sindri/workflow/instances/{instanceId}/commands";

    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        // ---- P2: the registry (bound) ----
        Bound("RegisterRecipe", typeof(RegisterRecipeRequest), ActionClass.Desk, "POST /sindri/workflow/recipes", "sindri recipe register <file>") with { Ui = "ready" },
        Protected("ApproveRecipe", "POST /sindri/workflow/recipes/{id}@{hash}/approve", "sindri recipe approve <recipe>", "WF-P2") with { CommandType = typeof(ApproveRecipeRequest), Ui = "ready" },
        // A job (200 JobStarted like every job route, key 'approve-all', ARCH-CQRS-003: class 'job'); any caller may start it, with no typed challenge (owner decision q93).
        JobRows.Job("ApproveAllRecipes", typeof(ApproveAllRecipesRequest), "/sindri/workflow/recipes/approve-all", "sindri recipe approve-all") with { Ui = "ready" },
        Protected("RevokeRecipe", "POST /sindri/workflow/recipes/{id}@{hash}/revoke", "sindri recipe revoke <recipe>", "WF-P2") with { CommandType = typeof(RevokeRecipeRequest), Ui = "ready" },
        // The app's own challenge route (setup-once spec §4 item 1): a desk row, the human token only, for exactly three actions.
        Ready("IssueAppChallenge", typeof(IssueAppChallengeRequest), ActionClass.Desk, "POST /sindri/workflow/challenges/app", "sindri recipe approve <recipe>"),
        Protected("IssueChallenge", "POST /sindri/workflow/challenges", "sindri recipe approve <recipe>", "WF-P2") with { CommandType = typeof(IssueChallengeRequest), Class = ActionClass.Issue, Ui = "ready" },

        // ---- P4: instances (bound and mapped), and P5's takeover and handback ----
        Ready("StartInstance", typeof(StartInstanceRequest), ActionClass.Desk, "POST /sindri/workflow/instances", "sindri workflow start <recipe> --item <itemId>") with { ReadsViews = true }, // UI spec E-19: it waits for the launches view head
        // Run again: a desk row, not destructive, offered only on a root instance that ended failed, unresolved or cancelled (Reruns). Its CLI is the start's next attempt; the app's button also repeats the inputs.
        Ready("RerunInstance", typeof(RerunInstanceRequest), ActionClass.Desk, "POST /sindri/workflow/instances/{instanceId}/rerun", "sindri workflow start <recipe> --item <itemId> --attempt next"),
        Ready("IssueNonce", typeof(IssueNonceRequest), ActionClass.Issue, "POST /sindri/workflow/instances/{instanceId}/nonce", "sindri workflow command <address> <verb>"),
        Ready("ApplyCommand:answer-text", typeof(ApplyCommandRequest), ActionClass.Desk, Commands, "sindri workflow answer <address> --text <text>"),
        Ready("ApplyCommand:answer-resume", typeof(ApplyCommandRequest), ActionClass.Desk, Commands, "sindri workflow answer <address> --resume"),
        Ready("ApplyCommand:answer-reject", typeof(ApplyCommandRequest), ActionClass.Desk, Commands, "sindri workflow answer <address> --reject") with { Destructive = true },
        Protected("ApplyCommand:answer-grant", Commands, "sindri workflow answer <address> --grant <rounds>", "WF-P4") with { CommandType = typeof(ApplyCommandRequest), Ui = "ready" },
        Protected("ApplyCommand:answer-grant-tokens", Commands, "sindri workflow answer <address> --grant-tokens <tokens>", "WF-P4") with { CommandType = typeof(ApplyCommandRequest), Ui = "ready" },
        Protected("ApplyCommand:answer-accept", Commands, "sindri workflow answer <address> --accept", "WF-P4") with { CommandType = typeof(ApplyCommandRequest), Ui = "ready" },
        Ready("ApplyCommand:takeover", typeof(ApplyCommandRequest), ActionClass.Pause, Commands, "sindri workflow takeover <address>"),
        Ready("ApplyCommand:handback", typeof(ApplyCommandRequest), ActionClass.Desk, Commands, "sindri workflow handback <address>"),
        Ready("ApplyCommand:rewind", typeof(ApplyCommandRequest), ActionClass.Desk, Commands, "sindri workflow rewind <address> --to <state>"),
        Ready("ApplyCommand:cancel", typeof(ApplyCommandRequest), ActionClass.Desk, Commands, "sindri workflow cancel <address>") with { Destructive = true },

        // One row serves every recipe verb ('on: command.<verb>'): the verb is a field of the request's payload, so the
        // row's action stays 'ApplyCommand:command' whatever the recipe names (owner ruling, cross-lane). It is desk,
        // not destructive, and reads no views. A verb that needs another class or the destructive flag gets its own
        // 'ApplyCommand:command-<verb>' row instead (engine spec §6.2).
        Ready("ApplyCommand:command", typeof(ApplyCommandRequest), ActionClass.Desk, Commands, "sindri workflow command <address> <verb>"),
        Ready("CompleteStep", typeof(CompleteStepRequest), ActionClass.Desk, "POST /sindri/workflow/instances/{instanceId}/runs/{run}/complete",
            "sindri workflow complete <address> --run <token> --outcome <outcome>"),
        // A launched agent session's report (API spec §4.1): the same command with its launch secret, so a session passes the chain; the service checks the secret.
        Ready("CompleteStep:report", typeof(CompleteStepRequest), ActionClass.StepReport, "POST /sindri/workflow/instances/{instanceId}/runs/{run}/complete",
            "sindri workflow complete <address> --run <token> --outcome <outcome>"),
    ];

    /// <summary>A P4 or P5 row bound to its type and mapped: 'ready' in the app.</summary>
    private static CommandRow Ready(string action, Type type, ActionClass actionClass, string route, string cli) =>
        Pending(action, actionClass, route, cli, "WF-P4") with { CommandType = type, Ui = "ready" };

    /// <summary>A P2 row bound to its type, pending until its unit ships its endpoint.</summary>
    private static CommandRow Bound(string action, Type type, ActionClass actionClass, string route, string cli) =>
        Pending(action, actionClass, route, cli, "WF-P2") with { CommandType = type };

    /// <summary>
    /// A protected row: terminal-bound. It stays 'pending:&lt;phase&gt;' until its endpoint is mapped (every non-pending row's
    /// route must be, EndpointCqrsTests), then the unit that maps it sets its status to "ready".
    /// </summary>
    private static CommandRow Protected(string action, string route, string cli, string phase) =>
        Pending(action, ActionClass.Protected, route, cli, phase);

    /// <summary>A pending row: '&lt;Command&gt;[:&lt;variant&gt;]', its class, 'METHOD /route', its CLI form and the phase that ships it; no type yet.</summary>
    private static CommandRow Pending(string action, ActionClass actionClass, string route, string cli, string phase)
    {
        var colon = action.IndexOf(':', StringComparison.Ordinal);
        var space = route.IndexOf(' ', StringComparison.Ordinal);
        return new CommandRow(ActionId.From(action), CommandType: null, colon < 0 ? null : action[(colon + 1)..], actionClass,
            ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false,
            Shortcut: null, route[..space], route[(space + 1)..], ShellRows.Form(cli), "pending:" + phase);
    }
}
