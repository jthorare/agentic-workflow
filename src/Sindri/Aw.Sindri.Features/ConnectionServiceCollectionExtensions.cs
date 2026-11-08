using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RefreshConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the connection slices (connections PR 10a).</summary>
internal static class ConnectionServiceCollectionExtensions
{
    /// <summary>Adds AddConnection, TestConnection, RemoveConnection and SignInConnection behind the command chain and ListConnections and GetConnection behind the query chain (ARCH-CQRS-001, #2344). Agent sessions are allowed by the catalog rows' SessionOk (owner decision D2).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddConnectionSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IScheduledService, RefreshConnectionsService>()
            .AddSingleton(RefreshConnectionsService.BuiltIn)
            .AddScoped<ConnectionEvents>()
            .AddSingleton<SignInOutcomes>()
            .AddScoped<OAuthSignInFlow>()
            .AddScoped<IValidator<AddConnectionRequest>, AcceptAllValidator<AddConnectionRequest>>().AddCommand<AddConnectionRequest, AddConnectionResponse, AddConnectionService>()
            .AddScoped<IValidator<TestConnectionRequest>, AcceptAllValidator<TestConnectionRequest>>().AddCommand<TestConnectionRequest, TestConnectionResponse, TestConnectionService>()
            .AddScoped<IValidator<RemoveConnectionRequest>, AcceptAllValidator<RemoveConnectionRequest>>().AddCommand<RemoveConnectionRequest, RemoveConnectionResponse, RemoveConnectionService>()
            .AddScoped<IValidator<SignInConnectionRequest>, AcceptAllValidator<SignInConnectionRequest>>().AddCommand<SignInConnectionRequest, AddConnectionResponse, SignInConnectionService>()
            .AddScoped<IValidator<ListConnectionsRequest>, AcceptAllValidator<ListConnectionsRequest>>().AddQuery<ListConnectionsRequest, ListConnectionsResponse, ListConnectionsService>()
            .AddScoped<IValidator<GetConnectionRequest>, AcceptAllValidator<GetConnectionRequest>>().AddQuery<GetConnectionRequest, GetConnectionResponse, GetConnectionService>();
    }
}
