using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.ListSessions;

/// <summary>The sandbox facts a session read test starts a launch with and expects back.</summary>
internal static class SandboxData
{
    public const string Folder = "/work/api/.claude/worktrees/ideas";
    public const long Gib = 1024L * 1024 * 1024;

    /// <summary>What 'launch.started' records for a sandboxed Step: 12 GiB of memory.</summary>
    public static SandboxSummary Summary(bool attended = false) => new(
        SandboxMechanism.Bubblewrap, "9f2c0a41d8e6", [Folder], ["model"], NetworkMode.Enforce, InnerState.On, new SandboxLimitsSummary("12GiB", "400%", 2048),
        new SandboxCredentialsSummary(GithubCredentials.None), ModelLoginSource.OwnerCopy, attended);

    /// <summary>Asserts <paramref name="actual"/> is <see cref="Summary"/>: the record compares its lists by reference, so they are compared by content here.</summary>
    /// <param name="actual">The summary read back.</param>
    public static void AssertIsSummary(SandboxSummary? actual)
    {
        var expected = Summary();
        Assert.NotNull(actual);
        Assert.Equal(expected.Writable, actual.Writable);
        Assert.Equal(expected.Network, actual.Network);
        Assert.Equal(expected, actual with { Writable = expected.Writable, Network = expected.Network });
    }

    /// <summary>What the reader answers while the scope is alive: 2.1 GiB of 12 in use.</summary>
    public static SessionSandbox Reading(bool attended = false) =>
        new(SessionSandboxState.On, attended, Folder, new SessionMemory((long)(2.1 * Gib), 12 * Gib));
}
