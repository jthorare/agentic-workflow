using System.Diagnostics;
using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class ProcessLivenessTests
{
    [Fact, Trait("Lane", "unit")]
    public void This_process_is_alive() => Assert.True(ProcessLiveness.IsAlive(Environment.ProcessId));

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(2_147_000_000)]
    public void A_pid_no_process_has_is_not_alive(int pid) => Assert.False(ProcessLiveness.IsAlive(pid));

    [Fact, Trait("Lane", "integration")]
    public void A_process_that_has_exited_and_been_reaped_is_not_alive()
    {
        var info = new ProcessStartInfo("true") { UseShellExecute = false };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("true did not start");
        var pid = process.Id;
        process.WaitForExit();

        Assert.False(ProcessLiveness.IsAlive(pid));
    }
}
