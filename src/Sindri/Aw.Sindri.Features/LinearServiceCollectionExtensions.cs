using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ListLinearIssues;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Linear hub's reads (Linear hub spec §3, Ph3 1a).</summary>
internal static class LinearServiceCollectionExtensions
{
    /// <summary>Adds ListLinearIssues behind the query chain, its facts over Context and the item links over the 'items' view.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLinearQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ListLinearIssuesRequest>, AcceptAllValidator<ListLinearIssuesRequest>>().AddQuery<ListLinearIssuesRequest, ListLinearIssuesResponse, ListLinearIssuesService>()
            .AddScoped<ILinearIssueFacts, ContextLinearIssueFacts>()
            .AddScoped<ILinearItemLinks, ViewLinearItemLinks>();
    }
}
