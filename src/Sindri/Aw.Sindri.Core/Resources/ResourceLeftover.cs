namespace Aw.Sindri.Core.Resources;

/// <summary>
/// A folder under the resources root that no listing shows: a purge's aside folder that could not be deleted or put back, or any other entry that is not a known kind folder.
/// </summary>
/// <param name="name">The entry's name under the resources root.</param>
/// <param name="owner">The owner the entry was staged for, or null when its name names none.</param>
/// <param name="sizeBytes">The bytes it holds now (0 when <paramref name="unmeasured"/>).</param>
/// <param name="stagedAt">When the entry was last written (the folder's mtime).</param>
/// <param name="path">The absolute path.</param>
/// <param name="unmeasured">True when the size could not be measured (too many files, SND-RES-007).</param>
public sealed class ResourceLeftover(string name, ResourceOwner? owner, long sizeBytes, DateTimeOffset stagedAt, string path, bool unmeasured = false)
{
    /// <summary>Gets the entry's name under the resources root.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the owner the entry was staged for, or null when its name names none.</summary>
    public ResourceOwner? Owner { get; } = owner;

    /// <summary>Gets the bytes it holds now.</summary>
    public long SizeBytes { get; } = sizeBytes;

    /// <summary>Gets when the entry was last written.</summary>
    public DateTimeOffset StagedAt { get; } = stagedAt;

    /// <summary>Gets the absolute path.</summary>
    public string Path { get; } = path;

    /// <summary>Gets a value indicating whether the size could not be measured.</summary>
    public bool Unmeasured { get; } = unmeasured;
}
