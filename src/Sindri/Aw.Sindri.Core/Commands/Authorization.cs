using Aw.Kernel;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// The authorization table (API spec §4.2) as data: which caller may run which action class. Pure; the command chain's
/// AuthorizingService asks it, and the command list renders <see cref="CallersFor(CommandRow)"/>. Every caller may run every class except two:
/// 'step-report' is a session's (its launch secret is checked by the service, API spec §8.1), and 'local' never runs through the API, so it refuses everyone.
/// A desk row may name the in-process services it grants ('service:host' → StopProcess). A session Sindri launched is no narrower than an owner-started one
/// (agent-first PR 2); the command list names both kinds of session 'session'. Attribution, not refusal, tells them apart.
/// </summary>
public static class Authorization
{
    private const string ServicePrefix = "service:";
    private const string HumanAtTerminal = "human+terminal";
    private const string HumanInApp = "human";

    private static readonly Dictionary<ActionClass, Who> _table = new()
    {
        [ActionClass.Request] = Who.Terminal | Who.App | Who.Session | Who.Launched | Who.Service,
        [ActionClass.StepReport] = Who.Session | Who.Launched,
        [ActionClass.Pause] = Who.Terminal | Who.App | Who.Session | Who.Launched | Who.Service,
        [ActionClass.Job] = Who.Terminal | Who.App | Who.Session | Who.Launched | Who.Service,
        [ActionClass.Decision] = Who.Terminal | Who.App | Who.Session | Who.Launched,
        [ActionClass.Desk] = Who.Terminal | Who.App | Who.Session | Who.Launched,
        [ActionClass.Local] = Who.None,
        [ActionClass.Issue] = Who.Terminal | Who.App | Who.Session | Who.Launched,
    };

    [Flags]
    private enum Who
    {
        None = 0,
        Terminal = 1,
        App = 2,
        Session = 4,
        Service = 8,
        Launched = 16,
    }

    /// <summary>Decides whether <paramref name="caller"/> may run <paramref name="row"/>, whose class is <paramref name="actionClass"/>.</summary>
    /// <param name="actionClass">The attempt's action class (the row's, after classification).</param>
    /// <param name="caller">The caller, or null when no caller context is set (always refused).</param>
    /// <param name="row">The catalog row: its service grants, terminal need, name and CLI form.</param>
    /// <returns>The decision.</returns>
    public static AuthorizationDecision Decide(ActionClass actionClass, CallerContext? caller, CommandRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var who = WhoIs(caller);
        var allowed = (Allowed(actionClass) & who) != Who.None || Granted(actionClass, caller, row);
        return new AuthorizationDecision(caller?.Actor ?? "none", allowed ? null : Refusal(row));
    }

    /// <summary>Who may run <paramref name="actionClass"/>: 'human+terminal', 'human', 'session', 'service' or 'service:&lt;name&gt;'.</summary>
    /// <param name="actionClass">The action class.</param>
    /// <param name="grants">The row's service grants (desk rows only).</param>
    /// <returns>The callers, in that order.</returns>
    public static IReadOnlyList<string> CallersFor(ActionClass actionClass, IReadOnlyList<string> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        var allowed = Allowed(actionClass);
        (Who Who, string Name)[] named = [(Who.Terminal, HumanAtTerminal), (Who.App, HumanInApp), (Who.Session, "session"), (Who.Service, "service")];
        var granted = actionClass == ActionClass.Desk ? grants.Select(static g => ServicePrefix + g) : [];
        return [.. named.Where(n => allowed.HasFlag(n.Who)).Select(static n => n.Name), .. granted];
    }

    /// <summary>Who may run <paramref name="row"/>, as the command list shows it.</summary>
    /// <param name="row">The catalog row.</param>
    /// <returns>The callers.</returns>
    public static IReadOnlyList<string> CallersFor(CommandRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return CallersFor(row.Class, row.ServiceGrants);
    }

    private static Who Allowed(ActionClass actionClass) => _table.GetValueOrDefault(actionClass, Who.None);

    private static Who WhoIs(CallerContext? caller) => caller switch
    {
        null => Who.None,
        { Class: CallerClass.Human, Terminal: true } => Who.Terminal,
        { Class: CallerClass.Human } => Who.App,
        { Class: CallerClass.Session, Launch: not null } => Who.Launched,
        { Class: CallerClass.Session } => Who.Session,
        { Class: CallerClass.Service } => Who.Service,
        _ => Who.None,
    };

    /// <summary>A service the desk row names: 'service:host' on StopProcess.</summary>
    private static bool Granted(ActionClass actionClass, CallerContext? caller, CommandRow row) =>
        actionClass == ActionClass.Desk
        && caller is { Class: CallerClass.Service, Actor: var actor }
        && actor.StartsWith(ServicePrefix, StringComparison.Ordinal)
        && row.ServiceGrants.Contains(actor[ServicePrefix.Length..], StringComparer.Ordinal);

    private static Error Refusal(CommandRow row) =>
        SindriErrors.AuthRefused.With($"{row.Action.Value} is not available to this caller: {row.Cli.Text}");
}
