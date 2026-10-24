using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Infrastructure.Desktop;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class ProcessDesktopAppsTests : IDisposable
{
    private readonly TempDirectory _root = new();

    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task The_app_runs_in_the_working_directory_with_the_runs_variables_and_says_why_it_ended()
    {
        var tool = StubCommand.Write(_root, "show.sh", "echo \"$APP_HOME|$1\" >&2\nexit 3");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["DISPLAY"] = ":0", ["APP_HOME"] = "/scratch/home" };

        var started = await new ProcessDesktopApps().StartAsync(new AppLaunch([tool, "arg"], _root.Path, environment), TestContext.Current.CancellationToken);

        await using var app = started.Value;
        var ended = await EndedWithAsync(app, "/scratch/home|arg");
        Assert.StartsWith("exited 3: ", ended, StringComparison.Ordinal);
        Assert.Equal("display: DISPLAY=:0 (the user's)", app.Display);
    }

    [Fact]
    public async Task Without_a_display_and_without_xvfb_run_the_app_is_not_started_and_the_error_says_so()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the virtual display is a Linux matter");
        var tool = StubCommand.Write(_root, "app.sh", "sleep 30");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["DISPLAY"] = string.Empty, ["PATH"] = _root.Path };

        var started = await new ProcessDesktopApps().StartAsync(new AppLaunch([tool], _root.Path, environment), TestContext.Current.CancellationToken);

        Assert.Contains("no DISPLAY and no xvfb-run", started.Error.Message, StringComparison.Ordinal);
    }

    // The app's standard error arrives on a reader thread: wait until its line is in the tail.
    private static async Task<string> EndedWithAsync(IRunningApp app, string line)
    {
        for (var tries = 0; tries < 200; tries++)
        {
            if (app.Ended() is { } ended && ended.Contains(line, StringComparison.Ordinal))
            {
                return ended;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return app.Ended() ?? "still running";
    }
}
