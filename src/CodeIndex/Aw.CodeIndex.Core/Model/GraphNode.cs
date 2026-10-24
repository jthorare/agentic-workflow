namespace Aw.CodeIndex.Core.Model;

/// <summary>A node of graphify's graph, kept as it came, trimmed (the 'graph_nodes' table). Untrusted: the file must name a path in the snapshot, and names are scrubbed and cut.</summary>
/// <param name="NodeKey">graphify's node key.</param>
/// <param name="File">The snapshot path, or null.</param>
/// <param name="Name">The scrubbed name, or null.</param>
/// <param name="Line">The line, or null.</param>
/// <param name="Community">The community number, or null.</param>
/// <param name="IsClass">True when graphify marked the node a class (a type, not a function): it is stored class-like and embedded by its name alone (no body).</param>
public sealed record GraphNode(string NodeKey, string? File, string? Name, int? Line, int? Community, bool IsClass = false);
