namespace Aw.Sindri.Core.Resources;

/// <summary>One provisioned resource found on disk: a database file or a disk folder under an owner's folder.</summary>
/// <param name="owner">The owner.</param>
/// <param name="kind">The kind.</param>
/// <param name="name">The resource's name (the file name without '.db', or the folder name).</param>
/// <param name="sizeBytes">The bytes it holds now.</param>
/// <param name="path">The absolute path.</param>
/// <param name="unmeasured">True when the size could not be measured (a disk with too many files, SND-RES-007); <paramref name="sizeBytes"/> is then 0.</param>
public sealed class ResourceEntry(ResourceOwner owner, ResourceKind kind, string name, long sizeBytes, string path, bool unmeasured = false)
{
    /// <summary>Gets the owner.</summary>
    public ResourceOwner Owner { get; } = owner;

    /// <summary>Gets the kind.</summary>
    public ResourceKind Kind { get; } = kind;

    /// <summary>Gets the name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the bytes it holds now.</summary>
    public long SizeBytes { get; } = sizeBytes;

    /// <summary>Gets the absolute path.</summary>
    public string Path { get; } = path;

    /// <summary>Gets a value indicating whether the size could not be measured.</summary>
    public bool Unmeasured { get; } = unmeasured;
}
