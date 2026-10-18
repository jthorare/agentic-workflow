using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Viewers;

/// <summary>The short queries the viewers and the bin resolver make through a shell: bounded, and never a process left behind.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ViewerProcessTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-output-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_query_over_its_budget_is_killed_not_left_running()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "reads /proc");
        var pidFile = Path.Combine(_dir, "pid");

        var output = await ViewerProcess.Output("sh", ["-c", $"echo $$ > '{pidFile}'; sleep 30"], TimeSpan.FromMilliseconds(400), Token);

        Assert.Null(output);
        var proc = "/proc/" + (await File.ReadAllTextAsync(pidFile, Token)).Trim();
        for (var i = 0; i < 100 && Directory.Exists(proc) && !IsZombie(proc); i++)
        {
            await Task.Delay(20, Token);
        }

        Assert.True(!Directory.Exists(proc) || IsZombie(proc), "the query's shell is still running");
    }

    [Fact]
    public async Task A_child_that_holds_stdout_open_cannot_hold_the_call_past_its_budget()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var output = await ViewerProcess.Output("sh", ["-c", "sleep 4 & echo hi"], TimeSpan.FromMilliseconds(500), Token);

        Assert.Null(output);   // the pipe never closed in time
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task A_query_that_answers_returns_its_trimmed_stdout() =>
        Expect.Equal("hello", await ViewerProcess.Output("sh", ["-c", "echo hello"], TimeSpan.FromSeconds(5), Token));

    [Theory]
    [InlineData("/usr/bin/claude", "/usr/bin/claude")]
    [InlineData("welcome back\nsome profile noise\n/usr/local/bin/claude\n", "/usr/local/bin/claude")]
    [InlineData("\n\n", null)]
    [InlineData(null, null)]
    public void The_resolved_bin_is_the_last_line_of_the_login_shells_output(string? output, string? expected) =>
        Expect.Equal(expected, BinResolver.LastLine(output));

    private static bool IsZombie(string proc)
    {
        try
        {
            return File.ReadAllText(proc + "/stat").Contains(") Z ", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return true;   // gone between the check and the read
        }
    }
}
