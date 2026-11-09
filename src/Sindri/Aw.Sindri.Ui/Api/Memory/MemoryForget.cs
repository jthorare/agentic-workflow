namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>What DELETE /sindri/memories/{name}/facts forgets: one fact, every fact with a tag, or the expired ones.</summary>
/// <param name="Name">The store.</param>
/// <param name="FactId">One fact's key, or null.</param>
/// <param name="Tag">Every fact with this tag, or null.</param>
/// <param name="Expired">True forgets the expired facts.</param>
internal sealed record MemoryForget(string Name, string? FactId = null, string? Tag = null, bool Expired = false);
