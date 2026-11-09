namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>POST /sindri/memories/{name}/facts's body.</summary>
/// <param name="Text">The fact's text.</param>
/// <param name="Tags">The tags, or null.</param>
/// <param name="ExpiresAt">When it expires, or null.</param>
/// <param name="Provenance">Where it came from, or null.</param>
internal sealed record WriteMemoryFactBody(string Text, IReadOnlyList<string>? Tags, DateTimeOffset? ExpiresAt, MemoryProvenance? Provenance);
