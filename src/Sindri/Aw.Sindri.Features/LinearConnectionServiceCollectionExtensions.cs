using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.LinearConnection;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers Settings, Connections, Linear (Linear hub spec 2026-11-04 §3): the connection read, the team list, the key save and the write key's removal.</summary>
internal static class LinearConnectionServiceCollectionExtensions
{
    /// <summary>Adds the four services behind their chains, and the facts, store and approval over Context and the profile. The Linear key checks are Host's.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLinearConnection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<GetLinearConnectionRequest>, AcceptAllValidator<GetLinearConnectionRequest>>().AddQuery<GetLinearConnectionRequest, LinearConnectionView, GetLinearConnectionService>()
            .AddScoped<IValidator<ListLinearTeamsRequest>, AcceptAllValidator<ListLinearTeamsRequest>>().AddCommand<ListLinearTeamsRequest, ListLinearTeamsResponse, ListLinearTeamsService>()
            .AddScoped<IValidator<SaveLinearConnectionRequest>, AcceptAllValidator<SaveLinearConnectionRequest>>().AddCommand<SaveLinearConnectionRequest, SaveLinearConnectionResponse, SaveLinearConnectionService>()
            .AddScoped<IValidator<RemoveLinearWriteKeyRequest>, AcceptAllValidator<RemoveLinearWriteKeyRequest>>().AddCommand<RemoveLinearWriteKeyRequest, RemoveLinearWriteKeyResponse, RemoveLinearWriteKeyService>()
            .AddScoped<ILinearConnectionFacts, ContextLinearConnectionFacts>()
            .AddScoped<ILinearConnectionStore, CoreLinearConnectionStore>()
            .AddScoped<ProfileChangeApproval>()
            .AddScoped(static sp => new LinearUrlGuard(sp.GetRequiredService<IProfileSource>(), sp.GetRequiredService<IApprovedProfile>()))
            .AddScoped<ILinearConnectionApproval, ProfileLinearConnectionApproval>();
    }
}
