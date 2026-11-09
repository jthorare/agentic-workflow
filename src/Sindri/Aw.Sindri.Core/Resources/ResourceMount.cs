namespace Aw.Sindri.Core.Resources;

/// <summary>A path the sandbox plan mounts for a run.</summary>
/// <param name="Path">The absolute path on the box.</param>
/// <param name="Writable">True for the read-write state mounts of databases and disks.</param>
public sealed record ResourceMount(string Path, bool Writable);
