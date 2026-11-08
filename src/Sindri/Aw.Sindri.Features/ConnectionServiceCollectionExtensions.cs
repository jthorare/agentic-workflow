using Aw.Kernel.Services;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the connection slices (connections PR 10a).</summary>
internal static class ConnectionServiceCollectionExtensions
{
    /// <summary>Adds AddConnection, TestConnection and RemoveConnection as plain services and ListConnections and GetConnection behind the query chain. Agent sessions are allowed (owner decision D2), so no authorization decorator.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddConnectionSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IService<AddConnectionRequest, AddConnectionResponse>, AddConnectionService>()
            .AddScoped<IService<TestConnectionRequest, TestConnectionResponse>, TestConnectionService>()
            .AddScoped<IService<RemoveConnectionRequest, RemoveConnectionResponse>, RemoveConnectionService>()
            .AddScoped<IValidator<ListConnectionsRequest>, AcceptAllValidator<ListConnectionsRequest>>().AddQuery<ListConnectionsRequest, ListConnectionsResponse, ListConnectionsService>()
            .AddScoped<IValidator<GetConnectionRequest>, AcceptAllValidator<GetConnectionRequest>>().AddQuery<GetConnectionRequest, GetConnectionResponse, GetConnectionService>();
    }
}
