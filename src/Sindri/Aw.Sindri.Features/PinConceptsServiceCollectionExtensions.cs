using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.PinConcepts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers PinConcepts (concept pins spec §3.6): the job that writes 'index.concepts'.</summary>
internal static class PinConceptsServiceCollectionExtensions
{
    /// <summary>Adds the PinConcepts job, keyed by a constant: one pin write at a time.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPinConcepts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddJob<PinConceptsRequest, PinConceptsResult, PinConceptsService>(static _ => JobKey.From("pins"), static r => "pinned " + string.Join(", ", r.Pins.Select(static p => p.Id.Value)));
    }
}
