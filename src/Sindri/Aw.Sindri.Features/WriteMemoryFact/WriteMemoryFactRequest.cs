using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.WriteMemoryFact;

/// <summary>Write a fact ('sindri memory write &lt;name&gt; &lt;text&gt; [--tag T]... [--expires-at D]'; POST /sindri/memories/{name}/facts; MCP memory_write).</summary>
/// <param name="Name">The memory.</param>
/// <param name="Text">The fact, 1 to 2,000 characters.</param>
/// <param name="Tags">The tags; null or empty is none.</param>
/// <param name="ExpiresAt">When the hourly service deletes it; null is never.</param>
/// <param name="Provenance">Where it came from; null is none.</param>
public sealed record WriteMemoryFactRequest(string Name, string Text, IReadOnlyList<string>? Tags, DateTimeOffset? ExpiresAt, MemoryProvenance? Provenance);
