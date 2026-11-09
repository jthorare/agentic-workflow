using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Writes a memory answer for a session caller with the text an agent may have written (facts, tags, provenance, descriptions) inside the untrusted fence. A port for the reason
/// <see cref="IProjectFencing"/> is one: the fence lives in the MCP namespace, which already depends on this one (ARCH-CYCLE-001).
/// </summary>
internal interface IMemoryFencing
{
    /// <summary>The stores as a session reads them.</summary>
    /// <param name="stores">The stores.</param>
    /// <returns>The JSON answer with the descriptions fenced.</returns>
    public IResult Stores(ListMemoriesResponse stores);

    /// <summary>A recall as a session reads it.</summary>
    /// <param name="recall">The recalled facts.</param>
    /// <returns>The JSON answer with text, tags and provenance fenced.</returns>
    public IResult Facts(SearchMemoryResponse recall);

    /// <summary>A new store as a session reads it.</summary>
    /// <param name="created">The store.</param>
    /// <returns>The JSON answer with the description fenced.</returns>
    public IResult Store(CreateMemoryResponse created);
}
