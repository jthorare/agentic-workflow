namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>One reading of a live scope's cgroup (sandbox spec §3.3): what the watchdog keeps the peaks of and the last of, because the files are gone when the scope is.</summary>
/// <param name="MemoryBytes">memory.current.</param>
/// <param name="MemoryPeakBytes">memory.peak, or the current value on a kernel without it.</param>
/// <param name="Events">memory.events.</param>
/// <param name="Tasks">pids.current.</param>
/// <param name="CpuMicroseconds">usage_usec of cpu.stat.</param>
internal sealed record ScopeSample(long MemoryBytes, long MemoryPeakBytes, MemoryEvents Events, int Tasks, long CpuMicroseconds);
