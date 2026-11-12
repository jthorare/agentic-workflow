using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The desktop app starts the 'sindri' beside it, once (first-run spec §4.1, decisions 2 and 2a; issue 3326).</summary>
[Lane(TestLane.Unit)]
public sealed class SindriStarterTests
{
    private static readonly string _app = Path.Combine(Path.GetTempPath(), "sindri-app");
    private static readonly string _home = Path.Combine(Path.GetTempPath(), "sindri-home");

    [Fact]
    public void Start_sindri_launches_the_binary_beside_the_app_once()
    {
        var process = new FakeProcessStarter();
        var starter = new SindriStarter(process, path => string.Equals(path, Path.Combine(_app, "sindri"), StringComparison.Ordinal), _app, _home);

        var first = starter.Start();
        var second = starter.Start();

        var launch = Assert.Single(process.Launches);
        Assert.Equal((Path.Combine(_app, "sindri"), "--headless", Path.Combine(_home, "logs", "serve.log")), (launch.Executable, Assert.Single(launch.Arguments), launch.LogPath));
        Assert.Equal((StartOutcome.Started, StartOutcome.AlreadyStarted), (first.Value, second.Value));
    }

    [Fact]
    public void With_no_sindri_beside_the_app_nothing_is_started()
    {
        var process = new FakeProcessStarter();
        var starter = new SindriStarter(process, _ => false, _app, _home);

        var result = starter.Start();

        Assert.Equal((StartOutcome.NotInstalled, 0), (result.Value, process.Launches.Count));
    }

    private sealed class FakeProcessStarter : IProcessStarter
    {
        public List<ProcessLaunch> Launches { get; } = [];

        public Result<Unit> Start(ProcessLaunch launch)
        {
            Launches.Add(launch);
            return Unit.Value;
        }
    }
}
