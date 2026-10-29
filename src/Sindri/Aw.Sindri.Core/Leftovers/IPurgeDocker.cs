using Aw.Kernel;

namespace Aw.Sindri.Core.Leftovers;

/// <summary>What the purge needs from Docker: list by compose label, remove by name. Self-contained: it shares nothing with the Onyx setup code, which is being deleted.</summary>
public interface IPurgeDocker
{
    /// <summary>Lists the containers (running or not) that carry the label com.docker.compose.project=<paramref name="project"/>. Never matches by name.</summary>
    /// <param name="project">The compose project name.</param>
    /// <returns>The container names; empty when Docker isn't installed.</returns>
    public Result<IReadOnlyList<string>> ContainersOf(string project);

    /// <summary>Lists the volumes that carry the same label.</summary>
    /// <param name="project">The compose project name.</param>
    /// <returns>The volume names; empty when Docker isn't installed.</returns>
    public Result<IReadOnlyList<string>> VolumesOf(string project);

    /// <summary>Runs `docker rm -f` on the containers.</summary>
    /// <param name="names">Container names from <see cref="ContainersOf"/>.</param>
    /// <returns>Ok, or SND-INDEX-014.</returns>
    public Result<Unit> RemoveContainers(IReadOnlyList<string> names);

    /// <summary>Runs `docker volume rm` on the volumes.</summary>
    /// <param name="names">Volume names from <see cref="VolumesOf"/>.</param>
    /// <returns>Ok, or SND-INDEX-014.</returns>
    public Result<Unit> RemoveVolumes(IReadOnlyList<string> names);
}
