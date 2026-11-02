namespace Aw.Sindri.Core.Sandbox;

/// <summary>One read of a scope's accounting files.</summary>
/// <param name="OomKills">'oom_kill' in memory.events: processes the kernel killed for memory.</param>
/// <param name="MemoryCurrent">memory.current, in bytes.</param>
/// <param name="MemoryPeak">memory.peak, in bytes; 0 where the kernel does not offer it.</param>
/// <param name="Pids">pids.current.</param>
/// <param name="CpuMicroseconds">'usage_usec' in cpu.stat.</param>
/// <param name="PidsMax">'max' in pids.events: forks refused because the scope was at its TasksMax.</param>
/// <param name="PidsPeak">pids.peak; 0 where the kernel does not offer it.</param>
public sealed record CgroupSample(long OomKills, long MemoryCurrent, long MemoryPeak, int Pids, long CpuMicroseconds, long PidsMax = 0, long PidsPeak = 0);
