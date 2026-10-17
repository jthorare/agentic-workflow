using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The same contract the fake is held to, with the adapter over a runner scripted from the facts.
[Lane(TestLane.Unit)]
public sealed class DockerCliRuntimeContractTests : ContainerRuntimeContract
{
    protected override IContainerRuntime Create(ContainerRuntimeFacts facts) => new DockerCliRuntime(new FakeToolRunner((_, arguments) => facts switch
    {
        { CliFound: false } => ToolRun.Missing,
        _ => arguments[0] switch
        {
            "version" => facts.DaemonProblem == DaemonProblem.Starting ? ToolRun.TimedOut : facts.DaemonAnswers ? new ToolRun(ToolOutcome.Exited, 0, facts.ServerVersion + "\n") : new ToolRun(ToolOutcome.Exited, 1, "", Stderr(facts.DaemonProblem)),
            "compose" => facts.ComposeV2 ? new ToolRun(ToolOutcome.Exited, 0, facts.ComposeVersion + "\n") : new ToolRun(ToolOutcome.Exited, 1, ""),
            _ => facts is { MemoryBytes: null, DataRoot: null, OperatingSystem: null }
                ? new ToolRun(ToolOutcome.Exited, 1, "")
                : new ToolRun(ToolOutcome.Exited, 0, facts.MemoryBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + facts.OperatingSystem + "|" + facts.DataRoot + "\n"),
        },
    }));

    // What the real docker CLI prints on stderr for each problem.
    private static string Stderr(DaemonProblem problem) => problem switch
    {
        DaemonProblem.PermissionDenied => "permission denied while trying to connect to the docker API at unix:///var/run/docker.sock\n",
        DaemonProblem.ContextMissing => "Failed to initialize: unable to resolve docker endpoint: context \"nope\": context not found: open /home/u/.docker/contexts/meta/x/meta.json: no such file or directory\n",
        DaemonProblem.None or DaemonProblem.NotRunning or DaemonProblem.Starting => "failed to connect to the docker API at unix:///var/run/docker.sock; check if the path is correct and if the daemon is running: dial unix /var/run/docker.sock: connect: no such file or directory\n",
        _ => throw new ArgumentOutOfRangeException(nameof(problem)),
    };
}
