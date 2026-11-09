using Aw.Context.Core.Facade;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetLinearLookups;
using Aw.Sindri.Features.GetLinearSince;
using Aw.Sindri.Features.LinearIssues;
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
            .AddScoped<ILinearSideFacts>(static sp => new ContextLinearSideFacts(   // why: the write scope is the Linear issues slice's check, handed over as a delegate so the slices stay apart
                sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<ICallerContextAccessor>(), sp.GetRequiredService<IContextEngine>(), sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<LinearScopes>().Write));
    }
}
