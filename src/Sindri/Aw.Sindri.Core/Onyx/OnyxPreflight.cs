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
    /// <param name="ownPort">The port our own running Onyx holds, when setup already ran: the port row is then ok at that port, taken or not.</param>
    /// <param name="usedByOthers">What the containers outside Onyx's project use of Docker's memory: null when it wasn't asked, an error when the read failed (the memory row then only warns).</param>
    /// <returns>The five checks and the port setup would use.</returns>
    public static PreflightReport Evaluate(ContainerRuntimeFacts docker, HostFacts host, OnyxPins pins, int? ownPort = null, Result<long>? usedByOthers = null)
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
            needsDocker ? Warn("memory", "needs docker") : Memory(docker, pins, usedByOthers),
            Disk(docker, host.Home, host.DataRoot, pins),
            ownPort is { } own ? Ok("port", $"{own.ToString(CultureInfo.InvariantCulture)} (Onyx's own)") : Port(host.Port),
        ];
        var port = host.Port;
        return new PreflightReport(checks, ownPort ?? (port.PreferredFree ? port.Preferred : port.FirstFreeFallback ?? port.Preferred));
    }

    /// <summary>Formats bytes as GiB with one decimal, the way the preflight prints them.</summary>
    /// <param name="bytes">A byte count.</param>
    /// <returns>For example 11.0.</returns>
    public static string Gib(long bytes) => (bytes / (double)(1L << 30)).ToString("F1", CultureInfo.InvariantCulture);

    private static PreflightCheck Docker(ContainerRuntimeFacts docker)
    {
        var (detail, problem) = JudgeDocker(docker);
        return problem is null ? Ok("docker", detail) : new PreflightCheck("docker", CheckState.Fail, detail, problem);
    }

    /// <summary>Judges whether Docker can be used here, in the order a person would fix it: no CLI, an endpoint on another machine, a daemon that doesn't answer, then an endpoint nobody could read.</summary>
    /// <param name="docker">What the container runtime reports.</param>
    /// <returns>The error (SND-ONYX-001 or SND-ONYX-010, with its fix), or null when Docker is usable here.</returns>
    public static Error? DockerProblem(ContainerRuntimeFacts docker) => JudgeDocker(docker).Problem;

    // The docker row's detail and its error come out of this one judgment, so the text a person reads and the code a caller branches on can't disagree.
    private static (string Detail, Error? Problem) JudgeDocker(ContainerRuntimeFacts docker)
    {
        ArgumentNullException.ThrowIfNull(docker);
        if (!docker.CliFound)
        {
            return ("docker CLI not found", SindriErrors.OnyxDockerUnavailable.With("Docker isn't installed: install Docker Desktop (or Docker Engine), then run this again"));
        }

        if (docker.Endpoint is not null && !IsLocalEndpoint(docker.Endpoint))
        {
            return ("docker points at another machine", RemoteDocker(docker.Endpoint));
        }

        if (!docker.DaemonAnswers)
        {
            return DaemonNotAnswering(docker);
        }

        return docker.Endpoint is null ? ("docker endpoint unreadable", RemoteDocker(endpoint: null)) : (docker.ServerVersion ?? "unreadable version", null);
    }

    // Why the daemon didn't answer, as far as the CLI's own message says: the row's detail and the one fix that applies.
    private static (string Detail, Error Problem) DaemonNotAnswering(ContainerRuntimeFacts docker) => docker.DaemonProblem switch
    {
        DaemonProblem.PermissionDenied => ("docker CLI found, permission denied on the daemon socket", SindriErrors.OnyxDockerUnavailable.With("Docker is running but this user can't open its socket: add your user to the docker group (`sudo usermod -aG docker $USER`, then log out and in), or use Docker Desktop, then run this again")),
        DaemonProblem.Starting => ($"Docker didn't answer within {(docker.WaitedSeconds ?? 0).ToString(CultureInfo.InvariantCulture)} s; it may be starting (Docker Desktop's Resource Saver)", SindriErrors.OnyxDockerUnavailable.With("Docker is slow to answer and may still be starting: wait a moment until it is ready, then run this again")),
        DaemonProblem.ContextMissing => ("docker CLI found, the selected docker context doesn't exist", SindriErrors.OnyxDockerUnavailable.With("The selected docker context doesn't exist: run `docker context use default` (`docker context ls` lists the others), then run this again")),
        DaemonProblem.None or DaemonProblem.NotRunning => ("docker CLI found, daemon not answering", SindriErrors.OnyxDockerUnavailable.With("Docker isn't running: start Docker Desktop (or the docker service) and wait until it is ready, then run this again")),
        _ => throw new ArgumentOutOfRangeException(nameof(docker), docker.DaemonProblem, "unknown daemon problem"),
    };

    /// <summary>Tells whether a Docker endpoint is on this machine: a unix socket, or a named pipe on Windows. An endpoint nobody could read (null) isn't, since it may be anywhere.</summary>
    /// <param name="endpoint">The endpoint from <see cref="ContainerRuntimeFacts.Endpoint"/>.</param>
    /// <returns>True for unix:// and npipe://.</returns>
    public static bool IsLocalEndpoint(string? endpoint)
        => endpoint is not null && (endpoint.StartsWith("unix://", StringComparison.Ordinal) || endpoint.StartsWith("npipe://", StringComparison.Ordinal));

    /// <summary>The error for a Docker that points at another machine: SND-ONYX-010 naming the endpoint and the fix.</summary>
    /// <param name="endpoint">The endpoint, or null when it couldn't be read (its own message: the context inspect failed).</param>
    /// <returns>The error.</returns>
    public static Error RemoteDocker(string? endpoint)
        => endpoint is null
            ? SindriErrors.OnyxRemoteDocker.With("Docker is there but Sindri couldn't read Docker's endpoint (`docker context inspect` failed); check `docker context ls`, then run this again")
            : SindriErrors.OnyxRemoteDocker.With($"Docker points at another machine ({endpoint}): Sindri only runs Onyx on this one; switch to the local context (`docker context use default`, and unset DOCKER_HOST), then run this again");

    /// <summary>The first Compose release that reads the `!override` tag Sindri's compose override uses.</summary>
    public static readonly Version ComposeFloor = new(2, 24, 0);

    private static PreflightCheck Compose(ContainerRuntimeFacts docker)
    {
        if (!docker.ComposeV2)
        {
            return Fail("compose", "Compose v2 not found", SindriErrors.OnyxComposeMissing, "Docker Compose v2 is missing (`docker compose version` fails; the legacy docker-compose isn't enough): update Docker Desktop or install the docker compose plugin");
        }

        if (docker.ComposeVersion is not { } text || !TryParseComposeVersion(text, out var version))
        {
            return Warn("compose", "unreadable version");   // the rendered compose is still checked before anything starts
        }

        return version >= ComposeFloor
            ? Ok("compose", text)
            : Fail("compose", text, SindriErrors.OnyxComposeMissing, $"Docker Compose {text} is too old: Sindri's override needs Compose {ComposeFloor} or newer (the `!override` tag); update Compose (Docker Desktop, or the docker compose plugin), then run this again");
    }

    /// <summary>Reads a Compose version such as 2.24.0, v2.29.7, 5.1.2 or 2.40.3-desktop.1 (the numbers before any suffix).</summary>
    /// <param name="text">The version text as `docker compose version --short` prints it, or null.</param>
    /// <param name="version">The major, minor and patch numbers.</param>
    /// <returns>True when the text starts with at least a major and a minor number.</returns>
    public static bool TryParseComposeVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        var core = text?.TrimStart('v', 'V').Split('-', '+')[0].Split('.');
        if (core is null || core.Length < 2 || !int.TryParse(core[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) || !int.TryParse(core[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        var patch = 0;
        if (core.Length > 2)
        {
            _ = int.TryParse(core[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch);
        }

        version = new Version(major, minor, patch);
        return true;
    }

    // The lock's docker-memory-bytes is a floor for `docker info` MemTotal, which reads a few percent under the VM limit
    // the user sets. The setting the user is told is that floor rounded up to a whole GiB (5.5 GiB floor, 6 GiB setting).
    private static PreflightCheck Memory(ContainerRuntimeFacts docker, OnyxPins pins, Result<long>? usedByOthers)
    {
        var setting = ((pins.DockerMemoryBytes + (1L << 30) - 1) >> 30).ToString(CultureInfo.InvariantCulture);
        if (docker.MemoryBytes is not { } have)
        {
            return Warn("memory", $"Docker's memory is unknown, needs a {setting} GiB setting");
        }

        var detail = $"{Gib(have)} GiB for Docker, needs a {setting} GiB setting";
        if (have < pins.DockerMemoryBytes)
        {
            return Fail("memory", detail, SindriErrors.OnyxNotEnoughResources, $"Docker has {Gib(have)} GiB and Onyx needs a {setting} GiB limit: set Docker's memory to at least {setting} GiB in Docker Desktop → Settings → Resources");
        }

        if (usedByOthers is not { } read)
        {
            return Ok("memory", detail);
        }

        // Docker's VM is shared: what other containers already hold is not there for Onyx (the floor is for one stack on a VM of its own).
        if (read.IsError)
        {
            return Warn("memory", $"{detail}; the memory other containers use couldn't be read");
        }

        var used = read.Value;
        var withOthers = $"{Gib(have)} GiB for Docker, {Gib(used)} GiB used by other containers, needs {Gib(pins.DockerMemoryBytes)} GiB free";
        return have - used >= pins.DockerMemoryBytes
            ? Ok("memory", withOthers)
            : Fail("memory", withOthers, SindriErrors.OnyxNotEnoughResources, $"Docker has {Gib(have)} GiB and other containers already use {Gib(used)} GiB, leaving {Gib(Math.Max(0, have - used))} GiB for Onyx, which needs {Gib(pins.DockerMemoryBytes)} GiB: stop the other containers (`docker ps`), or raise Docker's memory to at least {Gib(used + pins.DockerMemoryBytes)} GiB in Docker Desktop → Settings → Resources");
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
