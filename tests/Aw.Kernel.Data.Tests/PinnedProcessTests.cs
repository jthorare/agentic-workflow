using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel.Data.Processes;

namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class PinnedProcessTests
{
    [Fact]
    public async Task A_started_process_ends_with_its_own_exit_code()
    {
        var start = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("exit 3");

        using var pinned = await PinnedProcess.StartAsync(start);
        await pinned.Process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, pinned.Process.ExitCode);
    }

    [Fact]
    public void The_sync_start_gives_the_process_too_and_dispose_twice_is_safe()
    {
        var start = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("echo hi");

        var pinned = PinnedProcess.Start(start);
        var output = pinned.Process.StandardOutput.ReadToEnd().Trim();
        pinned.Dispose();
        pinned.Dispose();

        Assert.Equal("hi", output);
    }

    [Fact]
    public async Task A_missing_executable_surfaces_as_a_Win32Exception()
    {
        var start = new ProcessStartInfo("/nonexistent/aw-no-such-binary") { UseShellExecute = false };

        _ = await Assert.ThrowsAsync<Win32Exception>(() => PinnedProcess.StartAsync(start));
        _ = Assert.Throws<Win32Exception>(() => PinnedProcess.Start(start));
    }
}
