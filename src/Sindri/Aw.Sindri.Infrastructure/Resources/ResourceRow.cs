namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>One 'resource' row.</summary>
/// <param name="Owner">The owner, as 'workflow:slug'.</param>
/// <param name="Kind">The kind, as 'database' or 'disk'.</param>
/// <param name="Name">The name.</param>
/// <param name="Path">The file or folder.</param>
/// <param name="SizeLimit">A disk's limit in bytes, or null.</param>
internal sealed record ResourceRow(string Owner, string Kind, string Name, string Path, long? SizeLimit);
