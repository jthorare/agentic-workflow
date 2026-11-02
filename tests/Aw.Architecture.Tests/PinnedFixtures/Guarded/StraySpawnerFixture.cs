using System.Diagnostics;

namespace Aw.Architecture.Tests.PinnedFixtures.Guarded;

/// <summary>Calls Process.Start inside an async method next to the argv builder (an ARCH-PROC-002 fixture read as IL, never run).</summary>
internal static class StraySpawnerFixture
{
    internal static async Task<int> Run(ProcessStartInfo start)
    {
        await Task.Yield();
        using var process = Process.Start(start)!;
        return process.Id;
    }
}
