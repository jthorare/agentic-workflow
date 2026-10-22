using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The collector's process and socket sources follow the platform (collector spec §4.1, macOS).</summary>
public sealed class SessionSourceChoiceTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(false, true, false, false, false)]   // Linux: /proc
    [InlineData(false, true, true, true, false)]     // a /proc tree wins even on macOS
    [InlineData(false, false, true, true, true)]     // macOS: no /proc, ps there
    [InlineData(true, false, true, true, false)]     // AW_SESSIONS_PROC_ROOT is always /proc, a test override
    [InlineData(false, false, true, false, false)]   // macOS without ps: the /proc source says unsupported-platform
    [InlineData(false, false, false, true, false)]   // not macOS: its ps and lsof aren't known to read this way
    public void The_macOS_sources_are_for_macOS_without_a_proc_tree_and_with_ps(bool overridden, bool proc, bool mac, bool ps, bool expected) =>
        Assert.Equal(expected, SessionSourceChoice.UsesMac(overridden, proc, mac, ps));
}
