using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>The container runtime with scripted facts. Held to <see cref="ContainerRuntimeContract"/>.</summary>
/// <param name="facts">The facts it reports.</param>
public sealed class FakeContainerRuntime(ContainerRuntimeFacts facts) : IContainerRuntime
{
    /// <summary>A box with Docker Desktop 29.4.0 running with 16 GiB (its data lives in the VM), and Compose v2.40.3.</summary>
    public static ContainerRuntimeFacts Healthy { get; } = new(
        CliFound: true, DaemonAnswers: true, ServerVersion: "29.4.0", ComposeV2: true, ComposeVersion: "v2.40.3", MemoryBytes: 16_818_978_816, DataRoot: "/var/lib/docker", OperatingSystem: "Docker Desktop");

    /// <summary>A box with a native Linux engine: the same as <see cref="Healthy"/>, but its data root is a directory on this host.</summary>
    public static ContainerRuntimeFacts NativeEngine { get; } = Healthy with { OperatingSystem = "Ubuntu 24.04.1 LTS" };

    /// <summary>A box with no docker CLI.</summary>
    public static ContainerRuntimeFacts NoCli { get; } = new(
        CliFound: false, DaemonAnswers: false, ServerVersion: null, ComposeV2: false, ComposeVersion: null, MemoryBytes: null, DataRoot: null);

    /// <summary>A box with the CLI and Compose v2 whose daemon isn't answering (Docker Desktop not started).</summary>
    public static ContainerRuntimeFacts DaemonDown { get; } = Healthy with { DaemonAnswers = false, ServerVersion = null, MemoryBytes = null, DataRoot = null, OperatingSystem = null, DaemonProblem = DaemonProblem.NotRunning };

    /// <summary>A box whose daemon runs but whose socket this user may not open (a Linux user outside the docker group).</summary>
    public static ContainerRuntimeFacts PermissionDenied { get; } = DaemonDown with { DaemonProblem = DaemonProblem.PermissionDenied };

    /// <summary>A box whose daemon didn't answer in 25 s (5 s, then a 20 s retry): Docker Desktop waking from Resource Saver.</summary>
    public static ContainerRuntimeFacts Starting { get; } = DaemonDown with { DaemonProblem = DaemonProblem.Starting, WaitedSeconds = 25 };

    /// <summary>A box whose selected docker context doesn't exist.</summary>
    public static ContainerRuntimeFacts ContextMissing { get; } = DaemonDown with { DaemonProblem = DaemonProblem.ContextMissing };

    /// <summary>A box with Docker running and only legacy docker-compose.</summary>
    public static ContainerRuntimeFacts ComposeV1Only { get; } = Healthy with { ComposeV2 = false, ComposeVersion = null };

    /// <summary>Gets or sets the facts the fake reports.</summary>
    public ContainerRuntimeFacts Facts { get; set; } = facts;

    /// <inheritdoc />
    public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken) => Facts;
}
