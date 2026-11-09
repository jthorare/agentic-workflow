using Aw.Kernel.Services;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ExpireToolkitCalls;
using Aw.Sindri.Features.ListToolkitCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the toolkit call log slices (agent-first PR 16).</summary>
internal static class ToolkitCallServiceCollectionExtensions
{
    /// <summary>Adds ListToolkitCalls behind the query chain and the 'toolkit-calls-expire' built-in service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddToolkitCallSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListToolkitCallsRequest>, AcceptAllValidator<ListToolkitCallsRequest>>().AddQuery<ListToolkitCallsRequest, ListToolkitCallsResponse, ListToolkitCallsService>()
            .AddScoped<IScheduledService, ExpireToolkitCallsService>()
            .AddSingleton(ExpireToolkitCallsService.BuiltIn);   // runs without a profile entry, so PauseService refuses it
    }
}
