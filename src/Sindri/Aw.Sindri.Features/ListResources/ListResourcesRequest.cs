using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListResources;

/// <summary>List provisioned resources ('sindri resources list [owner]'; GET /sindri/resources?owner=; MCP resources_list).</summary>
/// <param name="Owner">One owner as 'workflow:&lt;slug&gt;', 'node:&lt;slug&gt;/&lt;id&gt;' or 'toolkit:&lt;name&gt;', or null for every owner.</param>
internal sealed record ListResourcesRequest(string? Owner = null) : IQuery<ListResourcesResponse>;
