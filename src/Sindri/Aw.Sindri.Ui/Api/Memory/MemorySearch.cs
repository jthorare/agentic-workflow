namespace Aw.Sindri.Ui.Api.Memory;

/// <summary>What GET /sindri/memories/{name}/search asks for.</summary>
/// <param name="Name">The store.</param>
/// <param name="Query">What to look for.</param>
/// <param name="Level">The recall level, or null.</param>
/// <param name="Limit">The most facts to return, or null.</param>
internal sealed record MemorySearch(string Name, string Query, string? Level = null, int? Limit = null);
