namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>What the argv builder needs to know about host paths: what one is, and where the symlinks in its parents lead.</summary>
internal interface IHostPaths
{
    /// <summary>Tells what the path is.</summary>
    /// <param name="path">A host path.</param>
    /// <returns>Missing, a directory or a file (or socket).</returns>
    public HostPathKind KindOf(string path);

    /// <summary>Gets the real user id of this process (from /proc/self/status, not the audit login uid).</summary>
    /// <returns>The uid, or null when it cannot be read.</returns>
    public int? Uid();

    /// <summary>Reads a small text file.</summary>
    /// <param name="path">A host path.</param>
    /// <returns>Its text, or null when it is not there or cannot be read.</returns>
    public string? ReadText(string path);

    /// <summary>Follows the symlinks in the path's parent directories (the last segment is left alone).</summary>
    /// <param name="path">An absolute host path.</param>
    /// <returns>The path with its parents resolved.</returns>
    public string Resolve(string path);
}
