using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// The container runtime Onyx runs on (Docker with Compose v2). <see cref="Inspect"/> only asks; the compose operations act on the
/// <see cref="ComposeProject"/>. Every compose operation returns SND-ONYX-001 when the CLI is missing or the daemon isn't answering and
/// SND-ONYX-011 (its message ends with the last lines the command printed) when the command fails; none throws except for a cancelled token.
/// </summary>
public interface IContainerRuntime
{
    /// <summary>Reads the runtime's facts. Never throws for a missing, stopped or hung runtime: that is what the facts say.</summary>
    /// <param name="cancellationToken">Cancels the inspection.</param>
    /// <returns>The facts.</returns>
    public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken);

    /// <summary>Reads the platform of the Docker server, which decides which digest of each image runs.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>linux/arm64 or linux/amd64 (aarch64 and x86_64 are normalized), or the error.</returns>
    public Result<string> ServerPlatform(CancellationToken cancellationToken);

    /// <summary>Pulls the project's images (`docker compose pull`), which can take a long time.</summary>
    /// <param name="project">The project.</param>
    /// <param name="progress">Called with each line the command prints on standard error while it runs.</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>Success, or the error.</returns>
    public Result<Unit> Pull(ComposeProject project, Action<string> progress, CancellationToken cancellationToken);

    /// <summary>Creates and starts the project's containers (`docker compose up -d --no-build`: the images are pulled, no build context exists on disk).</summary>
    /// <param name="project">The project.</param>
    /// <param name="progress">Called with each line the command prints on standard error while it runs.</param>
    /// <param name="limit">The most the command may run, at most the runtime's own limit; null for the runtime's own limit (a caller with a window to keep passes what is left of it).</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>Success, or the error (SND-ONYX-011 for a command stopped at its limit: <see cref="ComposeFailures.IsTimeout"/>).</returns>
    public Result<Unit> Up(ComposeProject project, Action<string> progress, TimeSpan? limit, CancellationToken cancellationToken);

    /// <summary>Stops the project's containers and keeps them, their volumes and their images (`docker compose stop`).</summary>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>Success, or the error.</returns>
    public Result<Unit> Stop(ComposeProject project, CancellationToken cancellationToken);

    /// <summary>Lists the project's containers, running or not.</summary>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>The containers, or the error.</returns>
    public Result<IReadOnlyList<ComposeContainer>> Ps(ComposeProject project, CancellationToken cancellationToken);

    /// <summary>Reads what the rendered compose would publish on the host and which services reach the host another way (`docker compose config --format json`: nothing is started or created).</summary>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>Every published port and host-access setting of every service in the compose's active profiles, or the error (SND-ONYX-011 when the output can't be read).</returns>
    public Result<ComposeConfigFacts> RenderedConfig(ComposeProject project, CancellationToken cancellationToken);

    /// <summary>Reads which repository digests each image has locally.</summary>
    /// <param name="images">Image references, such as docker.io/library/nginx@sha256:….</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>Each reference mapped to its RepoDigests as <see cref="ImageNames.WithoutDefaults"/> spells them; an image that isn't present maps to an empty list.</returns>
    public Result<IReadOnlyDictionary<string, IReadOnlyList<string>>> RepoDigests(IReadOnlyList<string> images, CancellationToken cancellationToken);

    /// <summary>Reads how much memory the running containers outside the project use (`docker stats --no-stream`, 5 s): what Docker's VM has already given away before Onyx starts.</summary>
    /// <param name="project">The project whose own containers are not counted.</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>The bytes the other containers use, or the error (SND-ONYX-001 when Docker isn't answering, SND-ONYX-011 when the command fails or its output can't be read).</returns>
    public Result<long> MemoryUsedOutside(ComposeProject project, CancellationToken cancellationToken);
}
