namespace Aw.Sindri.Core.Onyx;

/// <summary>What the box's container runtime reports (Docker today), as the Onyx preflight needs it.</summary>
/// <remarks>
/// A null <see cref="ServerVersion"/> or <see cref="ComposeVersion"/> beside a true <see cref="DaemonAnswers"/> or <see cref="ComposeV2"/> means
/// "not reported, or rejected as unsafe to print"; it does not say the version is old or new. P2 must add a distinct state for it before it compares versions.
/// </remarks>
/// <param name="CliFound">The docker CLI is on PATH.</param>
/// <param name="DaemonAnswers">The daemon answered `docker version` in time (false when it is down, still starting or hangs).</param>
/// <param name="ServerVersion">The daemon's version when it answers.</param>
/// <param name="ComposeV2">`docker compose version` works (the Compose v2 plugin; the legacy docker-compose v1 doesn't count).</param>
/// <param name="ComposeVersion">Compose's version when v2 is present.</param>
/// <param name="MemoryBytes">The memory Docker has (Docker Desktop's VM limit) when the daemon answers `docker info`.</param>
/// <param name="DataRoot">Docker's data root (`docker info` DockerRootDir) when the daemon answers: a host directory on a Linux engine, a path inside the VM on Docker Desktop.</param>
/// <param name="OperatingSystem">The engine's operating system (`docker info` OperatingSystem) when the daemon answers: "Docker Desktop" or "OrbStack" for a VM runtime, the distribution's name for a Linux engine.</param>
/// <param name="DaemonProblem">Why the daemon didn't answer, from the CLI's stderr, when <paramref name="DaemonAnswers"/> is false and the CLI is found; <see cref="DaemonProblem.None"/> when it answers.</param>
/// <param name="WaitedSeconds">How long the CLI waited in all for a daemon that timed out (<see cref="DaemonProblem.Starting"/>); null otherwise.</param>
public sealed record ContainerRuntimeFacts(
    bool CliFound, bool DaemonAnswers, string? ServerVersion, bool ComposeV2, string? ComposeVersion, long? MemoryBytes, string? DataRoot, string? OperatingSystem = null,
    DaemonProblem DaemonProblem = DaemonProblem.None, int? WaitedSeconds = null)
{
    /// <summary>Gets a value indicating whether Docker's data lives in a VM (Docker Desktop, OrbStack), so the data root is not a directory on this host even if a directory of that name exists here.</summary>
    public bool DataInVm => OperatingSystem is { } os
        && (os.Contains("Desktop", StringComparison.OrdinalIgnoreCase) || os.Contains("OrbStack", StringComparison.OrdinalIgnoreCase));
}
