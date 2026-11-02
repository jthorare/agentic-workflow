using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.ListProjects;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

internal static partial class SindriTools
{
    /// <summary>Runs sindri_projects: the page, or one card when 'project' is given (as GET /sindri/projects/{project}).</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallProjects(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.Only(Projects, arguments, ["all", "project", .. McpArguments.PageNames]) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var page = ReadPage(arguments);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var project = McpArguments.OptionalString(Projects, arguments, "project");
        if (project.IsError)
        {
            return McpResults.Failure(services, project.Error);
        }

        if (project.Value is { } typed)
        {
            var one = await services.GetRequiredService<IService<GetProjectRequest, GetProjectResponse>>().Handle(new GetProjectRequest(typed), ct).ConfigureAwait(false);
            return McpResults.From(services, one, McpSummaries.Project, FencePlan.OneProject, page.Value);
        }

        var all = McpArguments.OptionalBool(Projects, arguments, "all");
        if (all.IsError)
        {
            return McpResults.Failure(services, all.Error);
        }

        var answer = await services.GetRequiredService<IService<ListProjectsRequest, ListProjectsResponse>>().Handle(new ListProjectsRequest(all.Value == true), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Projects, FencePlan.Projects, page.Value);
    }
}
