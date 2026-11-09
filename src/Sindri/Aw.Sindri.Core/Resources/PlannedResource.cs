namespace Aw.Sindri.Core.Resources;

/// <summary>One database or disk to create.</summary>
/// <param name="Kind">Database or disk.</param>
/// <param name="Name">Its name.</param>
/// <param name="Path">The file (database) or folder (disk).</param>
/// <param name="SizeLimit">A disk's limit in bytes; null for a database.</param>
public sealed record PlannedResource(ResourceKind Kind, string Name, string Path, long? SizeLimit);
