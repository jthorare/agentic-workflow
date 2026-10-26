using System.Globalization;

namespace Aw.BugFix.Infrastructure;

/// <summary>BugFix's infrastructure settings, read from the environment Composition passes.</summary>
/// <param name="CheckTimeout">How long a test command may run before it is killed and counted as not run (AW_BUGFIX_CHECK_TIMEOUT_SECONDS, default 30 minutes).</param>
/// <param name="CheckGrace">How long the processes a test command started get to exit after SIGTERM, at the time limit and after the command exits, before they are killed (AW_BUGFIX_CHECK_GRACE_SECONDS, default 30 seconds).</param>
/// <param name="LockWait">How long a call waits for another call's state lock before giving up (AW_BUGFIX_LOCK_WAIT_SECONDS, default 30 seconds).</param>
/// <param name="FrozenVariables">The variables that decide which code a test command runs (PATH, HOME, and the names in AW_BUGFIX_CHECK_ENV), frozen with the check.</param>
/// <param name="PassedVariables">The variables passed to a test command as they are (TERM, LANG, …).</param>
/// <param name="FileLockingDisabled">Whether DOTNET_SYSTEM_IO_DISABLEFILELOCKING turns .NET's file locks off, which would let two calls write the state at once.</param>
internal sealed record BugFixSettings(
    TimeSpan CheckTimeout,
    TimeSpan CheckGrace,
    TimeSpan LockWait,
    IReadOnlyDictionary<string, string> FrozenVariables,
    IReadOnlyDictionary<string, string> PassedVariables,
    bool FileLockingDisabled = false)
{
    internal static BugFixSettings Read(Func<string, string?> environment)
    {
        var kept = (environment("AW_BUGFIX_CHECK_ENV") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(CheckEnvironment.MayKeep);
        var frozen = CheckEnvironment.Frozen.Concat(kept).ToHashSet(StringComparer.Ordinal);
        return new(
            Seconds(environment("AW_BUGFIX_CHECK_TIMEOUT_SECONDS")) ?? TimeSpan.FromMinutes(30),
            Seconds(environment("AW_BUGFIX_CHECK_GRACE_SECONDS")) ?? TimeSpan.FromSeconds(30),
            Seconds(environment("AW_BUGFIX_LOCK_WAIT_SECONDS")) ?? TimeSpan.FromSeconds(30),
            Values(environment, frozen),
            Values(environment, CheckEnvironment.PassedThrough.Where(name => !frozen.Contains(name))),   // a name frozen and passed through is frozen
            Enabled(environment("DOTNET_SYSTEM_IO_DISABLEFILELOCKING")));
    }

    private static Dictionary<string, string> Values(Func<string, string?> environment, IEnumerable<string> names)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (environment(name) is { } value)
            {
                values[name] = value;
            }
        }

        return values;
    }

    private static TimeSpan? Seconds(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;

    private static bool Enabled(string? flag) =>
        string.Equals(flag, "1", StringComparison.Ordinal) || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
}
