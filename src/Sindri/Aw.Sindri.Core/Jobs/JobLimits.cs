namespace Aw.Sindri.Core.Jobs;

/// <summary>The bounds of the job runtime (API P6 plan Q3, answered 2026-10-15).</summary>
public static class JobLimits
{
    /// <summary>Progress lines a job's view keeps (the last ones).</summary>
    public const int ViewLines = 200;

    /// <summary>Characters in one progress line, after scrubbing and escaping.</summary>
    public const int LineChars = 200;

    /// <summary>Progress events one job may record; after them one '(progress truncated)' line and the rest are dropped.</summary>
    public const int MaxProgressEvents = 2_000;

    /// <summary>Lines waiting for the writer; a full queue drops the oldest and counts it.</summary>
    public const int QueueLines = 256;

    /// <summary>Jobs running at once; the next start is SND-JOB-002.</summary>
    public const int MaxRunning = 64;

    /// <summary>Bytes of result JSON a job keeps; a larger result is dropped with a note.</summary>
    public const int ResultBytes = 16 * 1024;
}
