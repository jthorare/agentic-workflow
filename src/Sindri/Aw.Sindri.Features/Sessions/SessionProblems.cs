using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>The problem rows of a pass: a fixed sentence per code, never an exception message (collector spec §4.1, §9).</summary>
internal static class SessionProblems
{
    /// <summary>SND-SESSION-002 when the error carries a reason; any other failure (a cancelled read, say) is a failed 'processes' source, never -002.</summary>
    internal static SessionProblem ProcessTable(Error error) =>
        UnavailableReasons.TryFrom(error, out var reason) ? Unavailable("processes", reason) : Source("processes");

    internal static SessionProblem Disabled { get; } = Unavailable("collector", UnavailableReason.Disabled);

    private static SessionProblem Unavailable(string source, UnavailableReason reason) =>
        new(source, SindriErrors.SessionsUnavailable.Code, $"Sindri can't see agent sessions on this system: {UnavailableReasons.Text(reason)}") { Reason = reason };

    internal static SessionProblem TestOverride { get; } = new("collector", SindriErrors.SessionsTestOverride.Code, "A test override is set in this run: the sessions shown may be fixtures");

    /// <summary>The launcher.json row. A bad sessions section can still hold 'own' (a typo'd scope fails closed), and then the row says so instead of "default settings" (#4252).</summary>
    internal static SessionProblem Launcher(SessionScope scope) => scope == SessionScope.Own
        ? new("launcher.json", SindriErrors.LauncherInvalid.Code, "launcher.json isn't valid; sessions.scope is read as 'own', and the built-in agents and defaults are used for the rest")
        : new("launcher.json", SindriErrors.LauncherInvalid.Code, "launcher.json isn't valid; the built-in agents and default settings are used");

    internal static SessionProblem Source(string source) => new(source, SindriErrors.SessionSourceFailed.Code, $"The {source} source couldn't be read in the last pass");

    internal static SessionProblem Overrun { get; } = new("collector", SindriErrors.SessionSourceFailed.Code, "A pass took longer than its 500 ms budget") { IsOverrun = true };

    /// <summary>The overruns in a row from which the overrun row names the kept list's age.</summary>
    internal const int StaleAfter = 3;

    /// <summary>The overrun row once the list is stale: its age, or that no pass has made one yet (<paramref name="age"/> null).</summary>
    internal static SessionProblem Stale(int inARow, TimeSpan? age)
    {
        var passes = string.Create(CultureInfo.InvariantCulture, $"the last {inARow} passes took longer than their 500 ms budget");
        var text = age is { } old ? $"The agent list is from {Ago(old)}: {passes}" : $"No agent list yet: {passes}";
        return Overrun with { Text = text };
    }

    private static string Ago(TimeSpan age) => age.TotalSeconds switch
    {
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalSeconds} s ago"),
        < 3600 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes} min ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h ago"),
    };
}
