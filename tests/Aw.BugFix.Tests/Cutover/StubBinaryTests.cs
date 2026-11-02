using System.Diagnostics;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cutover;

/// <summary>Issue 1250: a stub written while a parallel test forks must still start (Linux refuses to exec a file a forked child holds open for writing: ETXTBSY).</summary>
[Lane(TestLane.Unit)]
public sealed class StubBinaryTests
{
    private const int Threads = 8;
    private const int StubsPerThread = 25;

    [Fact]
    public async Task A_stub_starts_at_once_even_while_other_threads_write_and_start_stubs()
    {
        if (OperatingSystem.IsWindows())
        {
            return;   // the stubs are bash scripts, and ETXTBSY is a Unix error
        }

        var failures = new List<string>();
        var workers = Enumerable.Range(0, Threads).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < StubsPerThread; i++)
            {
                using var stub = new StubBinary("judge", "#!/bin/bash\nexit 0\n");
                try
                {
                    using var process = Process.Start(new ProcessStartInfo(stub.Path) { UseShellExecute = false })!;
                    await process.WaitForExitAsync(TestContext.Current.CancellationToken);
                }
                catch (System.ComponentModel.Win32Exception e)
                {
                    lock (failures)
                    {
                        failures.Add("errno " + e.NativeErrorCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + e.Message);   // 26 is ETXTBSY
                    }
                }
            }
        }, TestContext.Current.CancellationToken));

        await Task.WhenAll(workers);

        Assert.Empty(failures);
    }
}
