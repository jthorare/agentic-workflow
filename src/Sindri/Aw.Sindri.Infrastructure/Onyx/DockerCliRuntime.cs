using System.Globalization;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IContainerRuntime over the docker CLI: read-only commands, each with a 5 s timeout (`docker version` gets one retry with 20 s when it times out), arguments as a list (never a shell string).</summary>
/// <param name="runner">Runs the docker CLI.</param>
/// <param name="environment">Reads an environment variable: DOCKER_HOST says where the CLI sends its commands.</param>
internal sealed partial class DockerCliRuntime(IToolRunner runner, Func<string, string?> environment) : IContainerRuntime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _retryTimeout = TimeSpan.FromSeconds(20);

    /// <inheritdoc />
    public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken)
    {
        var server = Docker(["version", "--format", "{{.Server.Version}}"]);
        if (server.Outcome == ToolOutcome.TimedOut)
        {
            // A Docker Desktop waking from Resource Saver can take longer than the first budget: one retry with a longer one.
            server = runner.Run("docker", ["version", "--format", "{{.Server.Version}}"], _retryTimeout, cancellationToken: cancellationToken);
        }

        if (server.Outcome == ToolOutcome.Missing)
        {
            return new ContainerRuntimeFacts(CliFound: false, DaemonAnswers: false, ServerVersion: null, ComposeV2: false, ComposeVersion: null, MemoryBytes: null, DataRoot: null, Endpoint: null);
        }

        var serverText = Text(server);
        cancellationToken.ThrowIfCancellationRequested();
        var compose = Docker(["compose", "version", "--short"]);
        var composeText = Text(compose);
        cancellationToken.ThrowIfCancellationRequested();
        var (memory, operatingSystem, dataRoot) = serverText is null ? (null, null, null) : Info(Text(Docker(["info", "--format", "{{.MemTotal}}|{{.OperatingSystem}}|{{.DockerRootDir}}"])));

        return new ContainerRuntimeFacts(
            CliFound: true, DaemonAnswers: serverText is not null, ServerVersion: Version(serverText), ComposeV2: composeText is not null, ComposeVersion: Version(composeText), MemoryBytes: memory, DataRoot: dataRoot, Endpoint: Endpoint(),
            OperatingSystem: operatingSystem, DaemonProblem: serverText is null ? Problem(server) : DaemonProblem.None,
            WaitedSeconds: serverText is null && server.Outcome == ToolOutcome.TimedOut ? (int)(_timeout + _retryTimeout).TotalSeconds : null);
    }

    // Where the CLI would send commands: DOCKER_HOST when set (it wins over a context), else the current context's endpoint. Only a plain token reaches a row
    // on the terminal: no control character or space, at most 256 long; anything else reads as unknown (null), which the preflight refuses like a remote one.
    private string? Endpoint()
    {
        var text = environment("DOCKER_HOST") is { Length: > 0 } host ? host : Text(Docker(["context", "inspect", "--format", "{{.Endpoints.docker.Host}}"]));
        return text is { Length: <= 256 } && text.All(static c => !char.IsControl(c) && !char.IsWhiteSpace(c)) ? text : null;
    }

    // One `docker info` line, MemTotal|OperatingSystem|DockerRootDir, split at the first two '|' (a path may hold one, a byte count never does). Each part is
    // guarded on its own: memory is a plain non-negative number, the operating system a short text with no control character, the data root an absolute
    // path with no control character; anything else reads as unknown.
    private static (long? Memory, string? OperatingSystem, string? DataRoot) Info(string? line)
    {
        var parts = line?.Split('|', 3) ?? [];
        if (parts.Length < 3)
        {
            return (null, null, null);
        }

        return (
            long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ? bytes : null,
            parts[1] is { Length: > 0 and <= 64 } os && !os.Any(char.IsControl) ? os : null,
            Path.IsPathRooted(parts[2]) && !parts[2].Any(char.IsControl) ? parts[2] : null);
    }

    // Why `docker version` failed, from the CLI's own stderr. The text is only matched, never shown (it may hold paths or anything else the CLI prints).
    private static DaemonProblem Problem(ToolRun failed) => failed.Error switch
    {
        _ when failed.Outcome == ToolOutcome.TimedOut => DaemonProblem.Starting,
        var e when e.Contains("permission denied", StringComparison.OrdinalIgnoreCase) => DaemonProblem.PermissionDenied,
        var e when e.Contains("context", StringComparison.OrdinalIgnoreCase)
            && (e.Contains("does not exist", StringComparison.OrdinalIgnoreCase) || e.Contains("not found", StringComparison.OrdinalIgnoreCase)) => DaemonProblem.ContextMissing,
        _ => DaemonProblem.NotRunning,
    };

    private ToolRun Docker(string[] arguments) => runner.Run("docker", arguments, _timeout);

    // A version is shown in a row on the terminal, so only a token of [0-9A-Za-z.+_-], 1 to 64 long, is passed on; anything else (escape sequences, spaces) reads as unreadable.
    private static string? Version(string? text) => text is not null && text.Length <= 64 && text.All(static c => c is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '.' or '+' or '_' or '-') ? text : null;

    private static string? Text(ToolRun run)
        => run is { Outcome: ToolOutcome.Exited, ExitCode: 0 } && run.Output.Trim() is { Length: > 0 } text ? text : null;
}
