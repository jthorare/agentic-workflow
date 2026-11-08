using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListMemories;

/// <summary>Every memory with its fact count ('sindri memory list'; GET /sindri/memories; MCP memory_list).</summary>
public sealed record ListMemoriesRequest : IQuery<ListMemoriesResponse>;
