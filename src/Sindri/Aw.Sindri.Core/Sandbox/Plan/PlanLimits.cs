namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The resolved limits of a plan (sandbox spec §3.3).</summary>
/// <param name="MemoryMaxBytes">The per-session memory ceiling.</param>
/// <param name="MemoryHighBytes">Where the session is throttled (80% of the ceiling).</param>
/// <param name="CpuQuotaPercent">CPU quota in percent of one CPU (cores times the share).</param>
/// <param name="TasksMax">The process limit.</param>
/// <param name="Sessions">The cap on concurrent sessions.</param>
/// <param name="TotalMemoryBytes">The parent slice's memory ceiling.</param>
/// <param name="HomeTmpfsBytes">The size of the home tmpfs.</param>
/// <param name="WallGrace">Added to the Step's timeout.</param>
/// <param name="ParkMax">The longest single suspension the loose ceiling allows.</param>
/// <param name="Wall">The wall-clock limit the watchdog enforces, or null when none applies.</param>
/// <param name="RuntimeMax">The scope's loose last-resort ceiling, or null.</param>
public sealed record PlanLimits(
    long MemoryMaxBytes,
    long MemoryHighBytes,
    int CpuQuotaPercent,
    int TasksMax,
    int Sessions,
    long TotalMemoryBytes,
    long HomeTmpfsBytes,
    TimeSpan WallGrace,
    TimeSpan ParkMax,
    TimeSpan? Wall,
    TimeSpan? RuntimeMax);
