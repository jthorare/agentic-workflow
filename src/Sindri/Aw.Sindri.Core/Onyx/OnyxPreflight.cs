using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The decision of whether this box can run the pinned Onyx (spec §4.1 step 1, §6). Pure: the facts come in, the report goes out.</summary>
public static class OnyxPreflight
{
    /// <summary>The port Onyx's web server wants on 127.0.0.1.</summary>
    public const int PreferredPort = 3000;

    /// <summary>The first fallback port.</summary>
    public const int FallbackFirst = 3001;

    /// <summary>The last fallback port, inclusive.</summary>
    public const int FallbackLast = 3010;

    /// <summary>Judges the facts against the pins.</summary>
    /// <param name="docker">What the container runtime reports.</param>
    /// <param name="host">What the host reports: the disk at the Sindri home, the disk at Docker's data root when that is a directory here, and the loopback ports.</param>
    /// <param name="pins">What the pinned release needs.</param>
    /// <returns>The five checks and the port setup would use.</returns>
    public static PreflightReport Evaluate(ContainerRuntimeFacts docker, HostFacts host, OnyxPins pins)
    {
        ArgumentNullException.ThrowIfNull(docker);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(pins);
        var dockerCheck = Docker(docker);
        var needsDocker = dockerCheck.State != CheckState.Ok;
        PreflightCheck[] checks =
        [
            dockerCheck,
            needsDocker ? Warn("compose", "needs docker") : Compose(docker),
            needsDocker ? Warn("memory", "needs docker") : Memory(docker, pins),
            Disk(docker, host.Home, host.DataRoot, pins),
            Port(host.Port),
        ];
        var port = host.Port;
        return new PreflightReport(checks, port.PreferredFree ? port.Preferred : port.FirstFreeFallback ?? port.Preferred);
    }

    /// <summary>Formats bytes as GiB with one decimal, the way the preflight prints them.</summary>
    /// <param name="bytes">A byte count.</param>
    /// <returns>For example 11.0.</returns>
    public static string Gib(long bytes) => (bytes / (double)(1L << 30)).ToString("F1", CultureInfo.InvariantCulture);

    private static PreflightCheck Docker(ContainerRuntimeFacts docker) => docker switch
    {
        { CliFound: false } => Fail("docker", "docker CLI not found", SindriErrors.OnyxDockerUnavailable, "Docker isn't installed: install Docker Desktop (or Docker Engine), then run this again"),
        { DaemonAnswers: false, DaemonProblem: DaemonProblem.PermissionDenied } => Fail("docker", "docker CLI found, permission denied on the daemon socket", SindriErrors.OnyxDockerUnavailable, "Docker is running but this user can't open its socket: add your user to the docker group (`sudo usermod -aG docker $USER`, then log out and in), or use Docker Desktop, then run this again"),
        { DaemonAnswers: false, DaemonProblem: DaemonProblem.Starting } => Fail("docker", $"Docker didn't answer within {(docker.WaitedSeconds ?? 0).ToString(CultureInfo.InvariantCulture)} s; it may be starting (Docker Desktop's Resource Saver)", SindriErrors.OnyxDockerUnavailable, "Docker is slow to answer and may still be starting: wait a moment until it is ready, then run this again"),
        { DaemonAnswers: false, DaemonProblem: DaemonProblem.ContextMissing } => Fail("docker", "docker CLI found, the selected docker context doesn't exist", SindriErrors.OnyxDockerUnavailable, "The selected docker context doesn't exist: run `docker context use default` (`docker context ls` lists the others), then run this again"),
        { DaemonAnswers: false } => Fail("docker", "docker CLI found, daemon not answering", SindriErrors.OnyxDockerUnavailable, "Docker isn't running: start Docker Desktop (or the docker service) and wait until it is ready, then run this again"),
        _ => Ok("docker", docker.ServerVersion ?? "unreadable version"),
    };

    private static PreflightCheck Compose(ContainerRuntimeFacts docker) => docker.ComposeV2
        ? Ok("compose", docker.ComposeVersion ?? "unreadable version")
        : Fail("compose", "Compose v2 not found", SindriErrors.OnyxComposeMissing, "Docker Compose v2 is missing (`docker compose version` fails; the legacy docker-compose isn't enough): update Docker Desktop or install the docker compose plugin");

    // The lock's docker-memory-bytes is a floor for `docker info` MemTotal, which reads a few percent under the VM limit
    // the user sets. The setting the user is told is that floor rounded up to a whole GiB (5.5 GiB floor, 6 GiB setting).
    private static PreflightCheck Memory(ContainerRuntimeFacts docker, OnyxPins pins)
    {
        var setting = ((pins.DockerMemoryBytes + (1L << 30) - 1) >> 30).ToString(CultureInfo.InvariantCulture);
        if (docker.MemoryBytes is not { } have)
        {
            return Warn("memory", $"Docker's memory is unknown, needs a {setting} GiB setting");
        }

        var detail = $"{Gib(have)} GiB for Docker, needs a {setting} GiB setting";
        return have >= pins.DockerMemoryBytes
            ? Ok("memory", detail)
            : Fail("memory", detail, SindriErrors.OnyxNotEnoughResources, $"Docker has {Gib(have)} GiB and Onyx needs a {setting} GiB limit: set Docker's memory to at least {setting} GiB in Docker Desktop → Settings → Resources");
    }

    // Spec §4.1 step 1: free disk at the Sindri home and, when Docker's data root is a directory on this host (a native engine), there too;
    // the smaller one counts. Whether the data lives in a VM comes from what `docker info` reports (Docker Desktop, OrbStack), never from
    // whether a directory of that name exists here. A VM's disk image sits on the host volume and has a cap of its own, which no host probe
    // can read: the row says so, and a same-named host directory is not measured.
    private static PreflightCheck Disk(ContainerRuntimeFacts docker, DiskFacts home, DiskFacts? dataRoot, OnyxPins pins)
    {
        var hostRoot = docker.DataInVm ? null : dataRoot;
        var measured = hostRoot is not null && hostRoot.FreeBytes < home.FreeBytes ? hostRoot : home;
        var note = docker.DataInVm ? "; Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources)" : "";
        var detail = $"{Gib(measured.FreeBytes)} GiB free at {measured.MeasuredPath}, needs {Gib(pins.DiskBytes)} GiB{note}";
        return measured.FreeBytes >= pins.DiskBytes
            ? Ok("disk", detail)
            : Fail("disk", detail, SindriErrors.OnyxNotEnoughResources, $"Not enough disk: {detail}. Free space on that volume, then run this again");
    }

    private static PreflightCheck Port(PortFacts port)
    {
        var preferred = port.Preferred.ToString(CultureInfo.InvariantCulture);
        if (port.PreferredFree)
        {
            return Ok("port", $"{preferred} free");
        }

        return port.FirstFreeFallback is { } fallback
            ? Ok("port", $"{preferred} taken; would use {fallback.ToString(CultureInfo.InvariantCulture)}")
            : Fail(
                "port",
                $"{preferred} taken, no free port in {FallbackFirst.ToString(CultureInfo.InvariantCulture)}-{FallbackLast.ToString(CultureInfo.InvariantCulture)}",
                SindriErrors.OnyxPortTaken,
                $"Port {preferred} is taken by something else and so is every port from {FallbackFirst.ToString(CultureInfo.InvariantCulture)} to {FallbackLast.ToString(CultureInfo.InvariantCulture)}: stop what uses them, then run this again");
    }

    private static PreflightCheck Ok(string name, string detail) => new(name, CheckState.Ok, detail, Problem: null);

    private static PreflightCheck Warn(string name, string detail) => new(name, CheckState.Warn, detail, Problem: null);

    private static PreflightCheck Fail(string name, string detail, ErrorDefinition code, string message)
        => new(name, CheckState.Fail, detail, code.With(message));
}
