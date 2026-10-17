namespace Aw.Sindri.Core.Onyx;

/// <summary>One container of the compose project, as `docker compose ps` reports it.</summary>
/// <param name="Service">The compose service name, such as nginx.</param>
/// <param name="State">running, exited, restarting, created and so on.</param>
/// <param name="Health">healthy, unhealthy or starting when the service has a health check, otherwise null.</param>
/// <param name="Image">The image the container runs, as Docker reports it (repository@digest for a pinned one).</param>
/// <param name="Publishers">The ports it publishes on the host (an exposed-only port isn't listed).</param>
public sealed record ComposeContainer(string Service, string State, string? Health, string Image, IReadOnlyList<ComposePublisher> Publishers)
{
    /// <summary>Whether a compose container in this state is live: anything but exited, created or dead, so running, restarting and paused all count. The one place that says what "up" means.</summary>
    /// <param name="state">The state `docker compose ps` reports.</param>
    /// <returns>True when the container is, or is trying to be, up.</returns>
    public static bool IsLive(string state) => state is not ("exited" or "created" or "dead");
}
