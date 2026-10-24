using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// One command catalog row (API spec §3.3, UI spec E-8): a command, or one of its variants, with what the chain, the UI
/// and the CLI need to know about it.
/// </summary>
/// <param name="Action">The action id: '&lt;Command&gt;' or '&lt;Command&gt;:&lt;variant&gt;'.</param>
/// <param name="CommandType">The request record that serves it, or null while its phase hasn't shipped (and for local verbs).</param>
/// <param name="Variant">The variant, or null for a plain command.</param>
/// <param name="Class">The action class.</param>
/// <param name="ServiceGrants">The in-process services allowed to run a desk row ('host' for StopProcess).</param>
/// <param name="NeedsTerminal">True when only a human at a terminal may run it.</param>
/// <param name="SelfAudited">True when its accepted path writes its own audit (StopProcess's 'process.stopped').</param>
/// <param name="Destructive">True when the UI confirms it first.</param>
/// <param name="ReadsViews">True when it waits for the views to reach the ledger head.</param>
/// <param name="Shortcut">Its default key in the app, or null.</param>
/// <param name="Method">The HTTP method, or null for a local verb.</param>
/// <param name="Route">The route under /sindri, or null for a local verb.</param>
/// <param name="Cli">The CLI form.</param>
/// <param name="Ui">The UI status: 'ready', 'pending:&lt;phase&gt;' or 'cli-only:&lt;reason&gt;'.</param>
/// <param name="AppConfirm">True on exactly the three rows the app may confirm (setup-once spec §4 item 7): the app passes the table, and the handler redeems the challenge before its first write.</param>
/// <param name="Confirm">How the app confirms it: 'typed' or 'held'; null on every other row.</param>
/// <param name="SessionOk">True on the context rows an agent session may also run (owner q93, 2026-10-23): the table adds sessions to the class's callers for this row only.</param>
public sealed record CommandRow(ActionId Action, Type? CommandType, string? Variant, ActionClass Class,
    IReadOnlyList<string> ServiceGrants, bool NeedsTerminal, bool SelfAudited, bool Destructive, bool ReadsViews,
    string? Shortcut, string? Method, string? Route, CliForm Cli, string Ui, bool AppConfirm = false, string? Confirm = null, bool SessionOk = false);
