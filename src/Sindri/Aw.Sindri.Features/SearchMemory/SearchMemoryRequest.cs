using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.SearchMemory;

/// <summary>Recall facts by meaning ('sindri memory search &lt;name&gt; &lt;query&gt; [--level L] [--limit N]'; GET /sindri/memories/{name}/search; MCP memory_search).</summary>
/// <param name="Name">The memory.</param>
/// <param name="Query">What to recall.</param>
/// <param name="Level">'loose', 'balanced' or 'strict'; null is balanced.</param>
/// <param name="Limit">At most this many facts; null is 10, the most is 50.</param>
public sealed record SearchMemoryRequest(string Name, string Query, string? Level, int? Limit) : IQuery<SearchMemoryResponse>;
