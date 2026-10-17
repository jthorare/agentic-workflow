using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>The container runtime with scripted facts. Held to <see cref="ContainerRuntimeContract"/>.</summary>
/// <param name="facts">The facts it reports.</param>
public sealed class FakeContainerRuntime(ContainerRuntimeFacts facts) : IContainerRuntime
{
    /// <summary>A box with Docker Desktop 29.4.0 running with 16 GiB (its data lives in the VM), and Compose v2.40.3.</summary>
    public static ContainerRuntimeFacts Healthy { get; } = new(
        CliFound: true, DaemonAnswers: true, ServerVersion: "29.4.0", ComposeV2: true, ComposeVersion: "v2.40.3", MemoryBytes: 16_818_978_816, DataRoot: "/var/lib/docker", Endpoint: "unix:///var/run/docker.sock", OperatingSystem: "Docker Desktop");

    /// <summary>A box with a native Linux engine: the same as <see cref="Healthy"/>, but its data root is a directory on this host.</summary>
    public static ContainerRuntimeFacts NativeEngine { get; } = Healthy with { OperatingSystem = "Ubuntu 24.04.1 LTS" };

    /// <summary>A box with no docker CLI.</summary>
    public static ContainerRuntimeFacts NoCli { get; } = new(
        CliFound: false, DaemonAnswers: false, ServerVersion: null, ComposeV2: false, ComposeVersion: null, MemoryBytes: null, DataRoot: null, Endpoint: null);

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

    /// <summary>Gets the operations called, in order: Inspect, ServerPlatform, Pull, RenderedConfig, Up, Stop, Ps, RepoDigests, MemoryUsedOutside.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Gets or sets a callback told the name of each operation as it starts, before it can fail or be cancelled (a test cancels a token from it).</summary>
    public Action<string>? OnCall { get; set; }

    /// <summary>Gets the containers of the project: <see cref="Up"/> fills them, <see cref="Stop"/> marks them exited.</summary>
    public IList<ComposeContainer> Containers { get; } = [];

    /// <summary>Gets the limit each <see cref="Up"/> call was given (TimeSpan.MaxValue when it was given none), in order.</summary>
    public IList<TimeSpan> UpLimits { get; } = [];

    /// <summary>Gets the compose project name of each compose operation called, in order.</summary>
    public IList<string> Projects { get; } = [];

    /// <summary>Gets the operations (Ps, Stop) called on a project that names no files (<see cref="ComposeProject.ByLabel"/>), in order.</summary>
    public IList<string> LabelOnlyCalls { get; } = [];

    /// <summary>Gets or sets the bytes the containers outside the project use, as <see cref="MemoryUsedOutside"/> reports.</summary>
    public long OutsideMemoryBytes { get; set; }

    /// <summary>Gets the services <see cref="Stop"/> leaves running (a container that ignores the stop).</summary>
    public IList<string> StubbornServices { get; } = [];

    /// <summary>Gets the services <see cref="Up"/> starts: the eight Onyx runs with no profile.</summary>
    public IList<string> StartedServices { get; } = ["api_server", "background", "cache", "inference_model_server", "nginx", "opensearch", "relational_db", "web_server"];

    /// <summary>Gets or sets the platform the server reports.</summary>
    public string Platform { get; set; } = "linux/arm64";

    /// <summary>Gets or sets the host port nginx is published on after <see cref="Up"/>.</summary>
    public int PublishedPort { get; set; } = 3000;

    /// <summary>Gets the standard error lines <see cref="Pull"/> and <see cref="Up"/> hand to their progress callback.</summary>
    public IList<string> ProgressLines { get; } = [];

    /// <summary>Gets the images present locally, each reference with its RepoDigests as Docker spells them.</summary>
    public IDictionary<string, string[]> LocalImages { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>Gets or sets what <see cref="RenderedConfig"/> answers for the ports for a project: by default nginx on loopback at <see cref="PublishedPort"/> and nothing else; a rig answers from the rendered .env, as Compose reads it.</summary>
    public Func<ComposeProject, IReadOnlyList<ComposePublishedPort>>? Config { get; set; }

    /// <summary>Gets the host-access settings <see cref="RenderedConfig"/> reports (none by default).</summary>
    public IList<ComposeHostAccess> HostAccess { get; } = [];

    private readonly Dictionary<string, Error> _failures = new(StringComparer.Ordinal);

    /// <summary>Makes the next call of an operation fail with an error (once).</summary>
    /// <param name="operation">The operation name, as listed in <see cref="Calls"/>.</param>
    /// <param name="error">The error it returns.</param>
    public void FailNext(string operation, Error error) => _failures[operation] = error;

    /// <inheritdoc />
    public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken)
    {
        Calls.Add("Inspect");
        return Facts;
    }

    /// <inheritdoc />
    public Result<string> ServerPlatform(CancellationToken cancellationToken) => Begin("ServerPlatform") is { } error ? error : Platform;

    /// <inheritdoc />
    public Result<Unit> Pull(ComposeProject project, Action<string> progress, CancellationToken cancellationToken)
    {
        Projects.Add(project.Name);
        if (Begin("Pull") is { } error)
        {
            return error;
        }

        cancellationToken.ThrowIfCancellationRequested();

        ProgressLines.ToList().ForEach(progress);
        return Results.Ok();
    }

    /// <inheritdoc />
    public Result<Unit> Up(ComposeProject project, Action<string> progress, TimeSpan? limit, CancellationToken cancellationToken)
    {
        Projects.Add(project.Name);
        UpLimits.Add(limit ?? TimeSpan.MaxValue);
        if (Begin("Up") is { } error)
        {
            return error;
        }

        cancellationToken.ThrowIfCancellationRequested();

        ProgressLines.ToList().ForEach(progress);
        Containers.Clear();
        foreach (var service in StartedServices)
        {
            Containers.Add(new ComposeContainer(service, "running", Health: null, $"docker.io/test/{service}@sha256:0", service == "nginx" ? [new ComposePublisher("127.0.0.1", PublishedPort, 80)] : []));
        }

        return Results.Ok();
    }

    /// <inheritdoc />
    public Result<Unit> Stop(ComposeProject project, CancellationToken cancellationToken)
    {
        Projects.Add(project.Name);
        TrackLabelOnly(project, "Stop");
        if (Begin("Stop") is { } error)
        {
            return error;
        }

        cancellationToken.ThrowIfCancellationRequested();
        for (var i = 0; i < Containers.Count; i++)
        {
            if (!StubbornServices.Contains(Containers[i].Service))
            {
                Containers[i] = Containers[i] with { State = "exited" };
            }
        }

        return Results.Ok();
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<ComposeContainer>> Ps(ComposeProject project, CancellationToken cancellationToken)
    {
        Projects.Add(project.Name);
        TrackLabelOnly(project, "Ps");
        return Begin("Ps") is { } error ? error : Results.Success<IReadOnlyList<ComposeContainer>>([.. Containers]);
    }

    /// <inheritdoc />
    public Result<ComposeConfigFacts> RenderedConfig(ComposeProject project, CancellationToken cancellationToken)
    {
        Projects.Add(project.Name);
        return Begin("RenderedConfig") is { } error
            ? error
            : new ComposeConfigFacts(Config?.Invoke(project) ?? [new ComposePublishedPort("nginx", "127.0.0.1", PublishedPort, 80)], [.. HostAccess]);
    }

    /// <inheritdoc />
    public Result<IReadOnlyDictionary<string, IReadOnlyList<string>>> RepoDigests(IReadOnlyList<string> images, CancellationToken cancellationToken)
    {
        if (Begin("RepoDigests") is { } error)
        {
            return error;
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> map = images.Distinct(StringComparer.Ordinal)
            .ToDictionary(static i => i, i => (IReadOnlyList<string>)(LocalImages.TryGetValue(i, out var digests) ? digests : []), StringComparer.Ordinal);
        return Results.Success(map);
    }

    /// <inheritdoc />
    public Result<long> MemoryUsedOutside(ComposeProject project, CancellationToken cancellationToken)
    {
        Projects.Add(project.Name);
        return Begin("MemoryUsedOutside") is { } error ? error : OutsideMemoryBytes;
    }

    private void TrackLabelOnly(ComposeProject project, string operation)
    {
        if (!project.HasFiles)
        {
            LabelOnlyCalls.Add(operation);
        }
    }

    // Records the call, then says why it can't run: a failure queued for it, or a runtime that isn't there.
    private Error? Begin(string operation)
    {
        Calls.Add(operation);
        OnCall?.Invoke(operation);
        if (_failures.Remove(operation, out var queued))
        {
            return queued;
        }

        return Facts is { CliFound: true, DaemonAnswers: true } ? null : SindriErrors.OnyxDockerUnavailable.With("Docker isn't running: start Docker Desktop (or the docker service), then run this again");
    }
}
