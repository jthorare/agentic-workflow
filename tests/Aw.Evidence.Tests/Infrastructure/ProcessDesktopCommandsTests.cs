using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Infrastructure.Desktop;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class ProcessDesktopCommandsTests : IDisposable
{
    private readonly TempDirectory _root = new();

    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task The_program_runs_in_the_working_directory_with_the_runs_variables_added_to_the_environment()
    {
        var tool = StubCommand.Write(_root, "show.sh", "printf '%s|%s|%s' \"$PWD\" \"$APP_HOME\" \"$1\"");
        var spec = new CommandSpec([tool, "arg one"], _root.Path, new Dictionary<string, string>(StringComparer.Ordinal) { ["APP_HOME"] = "/scratch/home" }, TimeSpan.FromSeconds(20));

        var outcome = await new ProcessDesktopCommands().RunAsync(spec, TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Value.ExitCode);
        Assert.EndsWith("|/scratch/home|arg one", outcome.Value.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(_root.Path), outcome.Value.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_program_that_runs_past_its_time_limit_is_a_run_failure()
    {
        var tool = StubCommand.Write(_root, "slow.sh", "sleep 30");
        var spec = new CommandSpec([tool], _root.Path, new Dictionary<string, string>(StringComparer.Ordinal), TimeSpan.FromMilliseconds(300));

        var outcome = await new ProcessDesktopCommands().RunAsync(spec, TestContext.Current.CancellationToken);

        Assert.Contains("did not finish within", outcome.Error.Message, StringComparison.Ordinal);
    }
}
