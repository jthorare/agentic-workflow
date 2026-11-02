using Aw.Kernel.Services;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Features.ListLinearProjects;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Features.ProjectFacts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the four project writes and the three reads (projects page spec §5.3, §6.2).</summary>
internal static class ProjectServiceCollectionExtensions
{
    /// <summary>Adds AttachProject, ChangeProject (with the led-notes writer behind it), DetachProject and HideProject behind the command chain, ListProjects, ListLinearProjects and GetProject behind the query chain, and the facts port.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddProjectCommands(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<AttachProjectRequest>, AcceptAllValidator<AttachProjectRequest>>().AddCommand<AttachProjectRequest, ProjectAccepted, AttachProjectService>().AddScoped<AttachProjectLookup>()
            .AddScoped<IValidator<ChangeProjectRequest>, AcceptAllValidator<ChangeProjectRequest>>().AddCommand<ChangeProjectRequest, ProjectAccepted, ChangeProjectService>()
            .AddScoped<LedProjectNotes>()
            .AddScoped<IValidator<DetachProjectRequest>, AcceptAllValidator<DetachProjectRequest>>().AddCommand<DetachProjectRequest, ProjectAccepted, DetachProjectService>()
            .AddScoped<IValidator<HideProjectRequest>, AcceptAllValidator<HideProjectRequest>>().AddCommand<HideProjectRequest, ProjectAccepted, HideProjectService>()
            .AddScoped<IValidator<ListProjectsRequest>, AcceptAllValidator<ListProjectsRequest>>().AddQuery<ListProjectsRequest, ListProjectsResponse, ListProjectsService>()
            .AddScoped<IValidator<ListLinearProjectsRequest>, AcceptAllValidator<ListLinearProjectsRequest>>().AddQuery<ListLinearProjectsRequest, ListLinearProjectsResponse, ListLinearProjectsService>()
            .AddScoped<IValidator<GetProjectRequest>, AcceptAllValidator<GetProjectRequest>>().AddQuery<GetProjectRequest, GetProjectResponse, GetProjectService>()
            .AddScoped<IProjectPages, ProjectPageReader>()
            .AddScoped<IProjectFacts, ContextProjectFacts>();
    }
}
