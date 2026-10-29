using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The Projects endpoints' fencing for a session: the answer as the MCP tool gives it (<see cref="McpFencing"/> with <see cref="FencePlan.Projects"/> or <see cref="FencePlan.OneProject"/>).</summary>
/// <param name="json">The host's JSON options.</param>
internal sealed class ProjectFencing(JsonSerializerOptions json) : IProjectFencing
{
    public IResult Page(ListProjectsResponse page) => Fenced(AwJson.Serialize(page, json), FencePlan.Projects);

    public IResult One(GetProjectResponse project) => Fenced(AwJson.Serialize(project, json), FencePlan.OneProject);

    private static Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult Fenced(byte[] body, FencePlan plan)
    {
        using var document = JsonDocument.Parse(body);
        return TypedResults.Text(McpFencing.Apply(document.RootElement, plan).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }
}
