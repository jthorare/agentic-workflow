using System.Diagnostics;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cutover;

/// <summary>Issue 1250: StubBinary warms its stub up before the test runs it, and that run must leave none of the files the tests read (calls, env).</summary>
[Lane(TestLane.Unit)]
public sealed class StubBinaryWarmupTests
{
    [Fact]
    public async Task The_warm_up_run_leaves_no_trace_and_the_first_real_run_is_logged_once()
    {
        if (OperatingSystem.IsWindows())
        {
            return;   // the stubs are bash scripts
        }

        using var stub = new StubBinary("judge", "#!/bin/bash\nprintf '%s\\n' \"$*\" >> @DIR@/calls\nenv | grep '^AW_' > @DIR@/env || true\n");

        Assert.False(File.Exists(Path.Combine(stub.Directory, "calls")), "the warm-up must exit before the body");
        Assert.False(File.Exists(Path.Combine(stub.Directory, "env")), "the warm-up must exit before the body");

        var info = new ProcessStartInfo(stub.Path) { UseShellExecute = false };
        info.ArgumentList.Add("resolution-check");
        using (var process = Process.Start(info)!)
        {
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal("resolution-check\n", stub.Read("calls"));
        Assert.DoesNotContain("AW_TEST_SCRIPT_WARMUP", stub.Read("env"), StringComparison.Ordinal);
    }
}
