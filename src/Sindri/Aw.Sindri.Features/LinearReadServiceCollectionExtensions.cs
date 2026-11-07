using Aw.Kernel.Services;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetLinearLookups;
using Aw.Sindri.Features.GetLinearSince;
using Aw.Sindri.Features.LinearSideFacts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Linear hub's two side reads (Linear hub spec 2026-11-04 §3).</summary>
internal static class LinearReadServiceCollectionExtensions
{
    /// <summary>Adds GetLinearLookups and GetLinearSince behind the query chain, and the facts port over Context.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLinearReads(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<GetLinearLookupsRequest>, AcceptAllValidator<GetLinearLookupsRequest>>().AddQuery<GetLinearLookupsRequest, LinearLookupsResponse, GetLinearLookupsService>()
            .AddScoped<IValidator<GetLinearSinceRequest>, AcceptAllValidator<GetLinearSinceRequest>>().AddQuery<GetLinearSinceRequest, LinearSinceResponse, GetLinearSinceService>()
            .AddScoped<ILinearSideFacts, ContextLinearSideFacts>();
    }
}
