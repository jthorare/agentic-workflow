using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AdoptToolkits;
using Aw.Sindri.Features.BuildToolkit;
using Aw.Sindri.Features.CreateToolkit;
using Aw.Sindri.Features.ListToolkits;
using Aw.Sindri.Features.PauseToolkit;
using Aw.Sindri.Features.ResumeToolkit;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Features.StopToolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the toolkit slices (agent-first PR 16): the build job, start, stop and list.</summary>
internal static class ToolkitServiceCollectionExtensions
{
    /// <summary>Adds the toolkit slices. The build job is keyed by toolkit name, so one toolkit builds at a time.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddToolkits(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddJob<BuildToolkitRequest, BuildToolkitResult, BuildToolkitService>(static r => JobKey.From("toolkit:" + r.Name), static r => "built " + r.Name)
            .AddJob<CreateToolkitRequest, CreateToolkitResult, CreateToolkitService>(static r => JobKey.From("toolkit:" + r.Name), static r => "created " + r.Name)
            .AddSingleton<ToolkitRecovery>()
            .AddSingleton<ToolkitAdoption>()
            .AddScoped<IValidator<StartToolkitRequest>, AcceptAllValidator<StartToolkitRequest>>().AddCommand<StartToolkitRequest, StartToolkitResponse, StartToolkitService>()
            .AddScoped<IValidator<StopToolkitRequest>, AcceptAllValidator<StopToolkitRequest>>().AddCommand<StopToolkitRequest, StopToolkitResponse, StopToolkitService>()
            .AddScoped<IValidator<PauseToolkitRequest>, AcceptAllValidator<PauseToolkitRequest>>().AddCommand<PauseToolkitRequest, PauseToolkitResponse, PauseToolkitService>()
            .AddScoped<IValidator<ResumeToolkitRequest>, AcceptAllValidator<ResumeToolkitRequest>>().AddCommand<ResumeToolkitRequest, ResumeToolkitResponse, ResumeToolkitService>()
            .AddScoped<IValidator<ListToolkitsRequest>, AcceptAllValidator<ListToolkitsRequest>>().AddQuery<ListToolkitsRequest, ListToolkitsResponse, ListToolkitsService>();
    }
}
