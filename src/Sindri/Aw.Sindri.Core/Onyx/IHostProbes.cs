namespace Aw.Sindri.Core.Onyx;

/// <summary>Reads of the host the container runtime doesn't answer: free disk and free loopback ports.</summary>
public interface IHostProbes
{
    /// <summary>Measures free disk at a path that may not exist yet.</summary>
    /// <param name="path">The path Onyx would live under.</param>
    /// <returns>The free bytes of the nearest existing ancestor (the path itself when it exists), and which one that was.</returns>
    public DiskFacts FreeDisk(string path);

    /// <summary>Measures free disk at a directory only when it exists on this host; it never walks up to an ancestor.</summary>
    /// <param name="path">The directory, such as Docker's data root.</param>
    /// <returns>The free bytes of that directory, or null when it isn't a directory on this host (it lives inside a VM, or is gone).</returns>
    public DiskFacts? FreeDiskIfExists(string path);

    /// <summary>Probes loopback ports: a connect to 127.0.0.1 and [::1], then a brief bind on 127.0.0.1. The fallback range is probed only when the preferred port is taken; a probe that can't tell counts as taken.</summary>
    /// <param name="preferred">The port Onyx wants.</param>
    /// <param name="fallbackFirst">The first port of the fallback range.</param>
    /// <param name="fallbackLast">The last port of the fallback range, inclusive.</param>
    /// <returns>Whether the preferred port is free and the first free fallback.</returns>
    public PortFacts Ports(int preferred, int fallbackFirst, int fallbackLast);
}
