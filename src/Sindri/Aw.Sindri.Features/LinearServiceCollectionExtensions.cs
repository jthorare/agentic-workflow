using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.LinearIssues;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Linear hub's reads (Linear hub spec §3, Ph3 1a) and writes (Ph3 1b).</summary>
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
            .AddScoped<IValidator<GetLinearIssueRequest>, AcceptAllValidator<GetLinearIssueRequest>>().AddQuery<GetLinearIssueRequest, GetLinearIssueResponse, GetLinearIssueService>()
            .AddScoped<LinearScopes>()
            .AddScoped<ILinearWriteGate, ContextLinearWriteGate>()
            .AddScoped<ILinearIssueFacts, ContextLinearIssueFacts>()
            .AddScoped<ILinearItemLinks, ViewLinearItemLinks>();
    }

    /// <summary>Adds ChangeLinearIssue behind the command chain, and the owner's scope its Context calls run under.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLinearWrites(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<ChangeLinearIssueRequest>, AcceptAllValidator<ChangeLinearIssueRequest>>().AddCommand<ChangeLinearIssueRequest, LinearChangeResult, ChangeLinearIssueService>()
            .AddScoped<ILinearWriteScope, ContextLinearWriteScope>();
    }
}
