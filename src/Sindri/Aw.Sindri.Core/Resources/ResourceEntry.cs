namespace Aw.Sindri.Core.Resources;

/// <summary>One provisioned resource found on disk: a database file or a disk folder under an owner's folder.</summary>
/// <param name="owner">The owner.</param>
/// <param name="kind">The kind.</param>
/// <param name="name">The resource's name (the file name without '.db', or the folder name).</param>
/// <param name="sizeBytes">The bytes it holds now.</param>
/// <param name="path">The absolute path.</param>
public sealed class ResourceEntry(ResourceOwner owner, ResourceKind kind, string name, long sizeBytes, string path)
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
}
