using System.Diagnostics;
using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Processes;

namespace Aw.Meta.Tests.Adapters;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DetachedWaiterLauncherTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Without_setsid_the_start_is_AW_META_010_and_nothing_is_started()
    {
        using var dir = new TempDirectory();
        var marker = Path.Combine(dir.Path, "started");
        var launcher = new DetachedWaiterLauncher(() => null, () => "/usr/bin/aw-meta");

        var result = await launcher.Start(["sh", "-c", "touch " + marker], TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, result.Error.Code);
        Assert.Contains("unavailable on", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("setsid not found", result.Error.Message, StringComparison.Ordinal);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(marker));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Under_the_dotnet_host_the_start_is_AW_META_009_and_nothing_is_started()
    {
        var launcher = new DetachedWaiterLauncher(() => "/usr/bin/setsid", () => "/usr/share/dotnet/dotnet");

        var result = await launcher.Start(["aw-meta", "lane-recycle", "meta", "--wait-idle"], TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.CommandNotStarted.Code, result.Error.Code);
        Assert.Contains("--detach needs the published aw-meta binary", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_process_path_is_AW_META_009_with_its_own_message()
    {
        var launcher = new DetachedWaiterLauncher(() => "/usr/bin/setsid", () => null);

        var result = await launcher.Start(["aw-meta", "lane-recycle", "meta", "--wait-idle"], TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.CommandNotStarted.Code, result.Error.Code);
        Assert.Contains("can't find the running aw-meta binary", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_path_that_only_contains_dotnet_is_not_taken_for_the_dotnet_host()
    {
        var launcher = new DetachedWaiterLauncher(() => "/usr/bin/true", () => "/home/x/.dotnet-tools/aw-meta");

        var result = await launcher.Start(["aw-meta", "lane-recycle", "meta", "--wait-idle"], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_real_launcher_starts_the_command_in_its_own_session()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && (File.Exists("/usr/bin/setsid") || File.Exists("/bin/setsid")), "Linux with setsid only");

        var result = await new DetachedWaiterLauncher().Start(["sleep", "30"], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        var pid = result.Value;
        try
        {
            var id = pid.ToString(CultureInfo.InvariantCulture);
            var command = (await File.ReadAllTextAsync("/proc/" + id + "/cmdline", TestContext.Current.CancellationToken)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("sleep", Path.GetFileName(command[0]));
            Assert.False(string.Equals(SessionOf("/proc/self/stat"), SessionOf("/proc/" + id + "/stat"), StringComparison.Ordinal));
        }
        finally
        {
            using var child = Process.GetProcessById(pid);
            child.Kill();
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    // Field 6 of /proc/<pid>/stat; the command name (field 2) is in parentheses and may hold spaces, so count from the last ')'.
    private static string SessionOf(string statPath)
    {
        var text = File.ReadAllText(statPath);
        var fields = text[(text.LastIndexOf(')') + 2)..].Split(' ');
        return fields[3];
    }
}
