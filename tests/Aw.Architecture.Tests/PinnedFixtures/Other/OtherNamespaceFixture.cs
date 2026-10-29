using System.Diagnostics;

namespace Aw.Architecture.Tests.PinnedFixtures.Other;

/// <summary>Calls Process.Start in a namespace with no --die-with-parent argv (an ARCH-PROC-002 fixture read as IL, never run).</summary>
internal static class OtherNamespaceFixture
{
    internal static int Run(ProcessStartInfo start)
    {
        using var process = Process.Start(start)!;
        return process.Id;
    }
}
