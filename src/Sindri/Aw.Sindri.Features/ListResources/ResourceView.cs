namespace Aw.Sindri.Features.ListResources;

/// <summary>One resource on the wire.</summary>
/// <param name="Owner">The owner, such as 'workflow:triage'.</param>
/// <param name="Kind">'database' or 'disk'.</param>
/// <param name="Name">The resource's name.</param>
/// <param name="Size">The bytes it holds now.</param>
/// <param name="Path">The absolute path the block or agent reads it at.</param>
/// <param name="LimitBytes">A disk's declared size limit in bytes; null for a database, or for a disk no declaration names (the graph spec declares no disks yet).</param>
internal sealed record ResourceView(string Owner, string Kind, string Name, long Size, string Path, long? LimitBytes = null);
