using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListToolkits;

/// <summary>Lists every toolkit Sindri knows ('sindri toolkit list'; GET /sindri/toolkits; MCP toolkit_list).</summary>
public sealed record ListToolkitsRequest : IQuery<ListToolkitsResponse>;
