namespace Aw.Sindri.Core.Toolkits;

/// <summary>One resource a toolkit declares: a database, a disk or a connection.</summary>
/// <param name="Kind">database, disk or connection.</param>
/// <param name="Name">The resource name.</param>
/// <param name="Size">A disk's size limit such as "1GiB"; null for the others.</param>
/// <param name="Variable">The environment variable that carries it.</param>
public sealed record ToolkitResource(string Kind, string Name, string? Size, string Variable);
