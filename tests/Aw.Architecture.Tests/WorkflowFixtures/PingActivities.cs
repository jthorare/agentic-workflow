namespace Aw.Architecture.Tests.WorkflowFixtures;

/// <summary>An activity the clean workflow fixture schedules (ARCH-WF-001 fixture, never run).</summary>
public static class PingActivities
{
    /// <summary>Does nothing.</summary>
    /// <returns>A finished task.</returns>
    public static Task Ping() => Task.CompletedTask;
}
