using System.Diagnostics;
using Aw.Kernel.Processes;

namespace Aw.Architecture.Tests.PinnedFixtures.Guarded;

/// <summary>Starts through PinnedProcess next to the argv builder (an ARCH-PROC-002 fixture read as IL, never run).</summary>
internal static class PinnedSpawnerFixture
{
    internal static async Task<int> Run(ProcessStartInfo start)
    {
        using var pinned = await PinnedProcess.StartAsync(start);
        return pinned.Process.Id;
    }
}
