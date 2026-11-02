using Aw.BugFix.Infrastructure;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

/// <summary>A check that runs a .NET apphost needs DOTNET_ROOT and XDG_RUNTIME_DIR, with no AW_BUGFIX_CHECK_ENV set up before the baseline (#1154).</summary>
[Lane(TestLane.Integration)]
public sealed class CheckEnvironmentTests : IDisposable
{
    private const string Key = "000102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void DOTNET_ROOT_and_XDG_RUNTIME_DIR_reach_the_check_without_AW_BUGFIX_CHECK_ENV_and_only_DOTNET_ROOT_is_frozen()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";
        var runner = new ProcessCheckRunner(
            BugFixSettings.Read(name => name switch { "PATH" => path, "DOTNET_ROOT" => "/opt/dotnet", "XDG_RUNTIME_DIR" => "/run/user/1000", _ => null }),
            new LocalFiles());
        var log = Path.Combine(_temp.Path, "env.log");

        var exit = runner.Run(["sh", "-c", "echo \"[${DOTNET_ROOT-unset}] [${XDG_RUNTIME_DIR-unset}]\""], _temp.Path, log, Key);

        Assert.Equal("[/opt/dotnet] [/run/user/1000]", File.ReadAllText(log).Trim());
        Assert.True(exit.Environment.ContainsKey("DOTNET_ROOT"));
        Assert.False(exit.Environment.ContainsKey("XDG_RUNTIME_DIR"));
    }
}
