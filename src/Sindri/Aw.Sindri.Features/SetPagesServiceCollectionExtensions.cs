using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.SetPages;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers SetPages (pages spec §3.1): the job that writes 'index.pages'.</summary>
internal static class SetPagesServiceCollectionExtensions
{
    /// <summary>Adds the SetPages job, keyed by a constant: one page write at a time. Its request-only checks run in the gate as a precheck and its profile and map checks as an admission, so both are real HTTP errors.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSetPages(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<PageEvents>()
            .AddScoped<IMapPageFacts, EngineMapPageFacts>()
            .AddScoped<IJobAdmission<SetPagesRequest>, SetPagesAdmission>()
            .AddJob<SetPagesRequest, SetPagesResult, SetPagesService>(static _ => JobKey.From("pages"), static r => "set pages " + string.Join(", ", r.Pages.Select(static p => $"{p.Action} {p.Id.Value}")), PagePlanner.Precheck);
    }
}
