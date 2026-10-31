using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.PinConcepts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers PinConcepts (concept pins spec §3.6): the job that writes 'index.concepts'.</summary>
internal static class PinConceptsServiceCollectionExtensions
{
    /// <summary>Adds the PinConcepts job, keyed by a constant: one pin write at a time. Its request-only checks run in the gate as a precheck and its profile checks as an admission, so both are real HTTP errors.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPinConcepts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<PinEvents>()
            .AddScoped<IMapFeatures, EngineMapFeatures>()
            .AddScoped<IJobAdmission<PinConceptsRequest>, PinConceptsAdmission>()
            .AddJob<PinConceptsRequest, PinConceptsResult, PinConceptsService>(static _ => JobKey.From("pins"), static r => "pinned " + string.Join(", ", r.Pins.Select(static p => p.Id.Value)), PinPlanner.Precheck);
    }
}
