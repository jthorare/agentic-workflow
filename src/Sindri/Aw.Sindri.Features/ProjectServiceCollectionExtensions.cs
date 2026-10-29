using Aw.Kernel.Services;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Features.HideProject;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the four project writes (projects page spec §5.3).</summary>
internal static class ProjectServiceCollectionExtensions
{
    /// <summary>Adds AttachProject, ChangeProject, DetachProject and HideProject behind the command chain.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddProjectCommands(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<AttachProjectRequest>, AcceptAllValidator<AttachProjectRequest>>().AddCommand<AttachProjectRequest, ProjectAccepted, AttachProjectService>()
            .AddScoped<IValidator<ChangeProjectRequest>, AcceptAllValidator<ChangeProjectRequest>>().AddCommand<ChangeProjectRequest, ProjectAccepted, ChangeProjectService>()
            .AddScoped<IValidator<DetachProjectRequest>, AcceptAllValidator<DetachProjectRequest>>().AddCommand<DetachProjectRequest, ProjectAccepted, DetachProjectService>()
            .AddScoped<IValidator<HideProjectRequest>, AcceptAllValidator<HideProjectRequest>>().AddCommand<HideProjectRequest, ProjectAccepted, HideProjectService>();
    }
}
