namespace Aw.Meta.Infrastructure.Files;

/// <summary>What vm_stat, sysctl vm.swapusage and the state disk say.</summary>
/// <param name="AvailableKb">Free, inactive and speculative memory in kilobytes.</param>
/// <param name="SwapTotalKb">Swap size in kilobytes.</param>
/// <param name="SwapUsedKb">Swap in use in kilobytes.</param>
/// <param name="DiskFreeBytes">Free bytes on the state disk.</param>
internal sealed record MacMemory(long AvailableKb, long SwapTotalKb, long SwapUsedKb, long DiskFreeBytes);
