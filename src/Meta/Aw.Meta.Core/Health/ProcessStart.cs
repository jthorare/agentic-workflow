namespace Aw.Meta.Core.Health;

/// <summary>The pid-reuse rule: a process that started after the file naming its pid was last written is not the process that file is about.</summary>
public static class ProcessStart
{
    /// <summary>How much later than the file a process may be read to have started and still be its owner: ps and /proc give whole seconds.</summary>
    public static TimeSpan Tolerance { get; } = TimeSpan.FromSeconds(2);

    /// <summary>Whether the process that started at <paramref name="startedAt"/> cannot be the one a file written at <paramref name="recordedAt"/> names.</summary>
    /// <param name="startedAt">When the running process started.</param>
    /// <param name="recordedAt">When the session or lock file was last written.</param>
    /// <returns>True when the pid was reused.</returns>
    public static bool IsReused(DateTimeOffset startedAt, DateTimeOffset recordedAt) => startedAt > recordedAt + Tolerance;
}
