using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The Linear issue list's fencing for a session (<see cref="McpFencing"/> with <see cref="FencePlan.LinearIssues"/>).</summary>
/// <param name="json">The host's JSON options.</param>
internal sealed class LinearIssueFencing(JsonSerializerOptions json) : ILinearIssueFencing
{
    public IResult List(ListLinearIssuesResponse list)
    {
        using var document = JsonDocument.Parse(AwJson.Serialize(list, json));
        return TypedResults.Text(McpFencing.Apply(document.RootElement, FencePlan.LinearIssues).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }
}
