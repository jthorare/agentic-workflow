using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.PurgeResources;

/// <summary>Delete an owner's resources ('sindri resources purge &lt;owner&gt;'; POST /sindri/resources/purge; MCP resources_purge).</summary>
/// <param name="Owner">The owner as 'workflow:&lt;slug&gt;', 'node:&lt;slug&gt;/&lt;id&gt;' or 'toolkit:&lt;name&gt;'.</param>
internal sealed record PurgeResourcesRequest(string Owner) : ICommand<PurgeResourcesResponse>;
