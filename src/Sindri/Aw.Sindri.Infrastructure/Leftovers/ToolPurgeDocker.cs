using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Leftovers;

/// <summary>IPurgeDocker over the `docker` CLI: every list is a label filter, never a name match.</summary>
/// <param name="runner">Runs `docker`.</param>
internal sealed class ToolPurgeDocker(IToolRunner runner) : IPurgeDocker
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    public Result<IReadOnlyList<string>> ContainersOf(string project)
        => List(["ps", "-a", "--filter", Label(project), "--format", "{{.Names}}"]);

    public Result<IReadOnlyList<string>> VolumesOf(string project)
        => List(["volume", "ls", "--filter", Label(project), "--format", "{{.Name}}"]);

    public Result<Unit> RemoveContainers(IReadOnlyList<string> names) => Remove(["rm", "-f", .. names], "the containers");

    public Result<Unit> RemoveVolumes(IReadOnlyList<string> names) => Remove(["volume", "rm", .. names], "the volumes");

    private static string Label(string project) => $"label=com.docker.compose.project={project}";

    private static Error Failed(string what) => SindriErrors.IndexOnyxLeftover.With($"{what} (fix: start Docker and run `sindri onyx down --purge` again)");

    // No docker installed means no containers or volumes on this box.
    private Result<IReadOnlyList<string>> List(string[] arguments)
    {
        var run = runner.Run("docker", arguments, _timeout);
        if (run.Outcome == ToolOutcome.Missing)
        {
            return Array.Empty<string>();
        }

        return run is { Outcome: ToolOutcome.Exited, ExitCode: 0 }
            ? run.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Failed("docker did not list what Onyx left");
    }

    private Result<Unit> Remove(string[] arguments, string what)
        => runner.Run("docker", arguments, _timeout) is { Outcome: ToolOutcome.Exited, ExitCode: 0 } ? Results.Ok() : Failed($"docker could not remove {what}");
}
