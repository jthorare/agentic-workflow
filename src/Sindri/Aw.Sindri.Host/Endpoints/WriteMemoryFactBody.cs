using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/memories/{name}/facts: the write request without the name the route carries.</summary>
/// <param name="Text">The fact.</param>
/// <param name="Tags">The tags, or null.</param>
/// <param name="ExpiresAt">When it expires, or null.</param>
/// <param name="Provenance">Where it came from, or null.</param>
internal sealed record WriteMemoryFactBody(string Text, IReadOnlyList<string>? Tags = null, DateTimeOffset? ExpiresAt = null, MemoryProvenance? Provenance = null);
