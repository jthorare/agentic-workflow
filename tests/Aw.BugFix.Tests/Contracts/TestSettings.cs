using Aw.BugFix.Infrastructure;

namespace Aw.BugFix.Tests.Contracts;

/// <summary>Infrastructure settings for the adapter tests: the given time limits, and this process's PATH and HOME for checks.</summary>
internal static class TestSettings
{
    private static readonly HashSet<string> _passed = new(StringComparer.Ordinal) { "PATH", "HOME" };

    public static BugFixSettings For(TimeSpan checkTimeout, TimeSpan lockWait, TimeSpan? checkGrace = null)
    {
        var settings = BugFixSettings.Read(static name => _passed.Contains(name) ? Environment.GetEnvironmentVariable(name) : null);
        return settings with { CheckTimeout = checkTimeout, LockWait = lockWait, CheckGrace = checkGrace ?? settings.CheckGrace, };
    }
}
