using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.CheckProfile;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features;

/// <summary>Registers <see cref="ProfileRecheck"/> and wraps the profile writer ports with it.</summary>
public static class ProfileRecheckServiceCollectionExtensions
{
    /// <summary>
    /// Adds the recheck and replaces each registered profile writer port that runs in serve (index, repo, sandbox) with a decorator that checks the profile after a write. Call it after the Infrastructure registrations: it wraps what is registered then, and
    /// leaves a port nobody registered alone.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddProfileRecheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddSingleton(static sp =>
            {
                var scopes = sp.GetRequiredService<IServiceScopeFactory>();
                return new ProfileRecheck(async (request, ct) =>
                {
                    var scope = scopes.CreateAsyncScope();
                    await using (scope.ConfigureAwait(false))
                    {
                        return await scope.ServiceProvider.GetRequiredService<IService<CheckProfileRequest, CheckProfileResponse>>().Handle(request, ct).ConfigureAwait(false);
                    }
                }, sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<ICallerContextAccessor>(), sp.GetRequiredService<ILogger<ProfileRecheck>>());
            })
            .Decorate<IProfileIndexWriter>(static (inner, recheck) => new RecheckingProfileIndexWriter(inner, recheck))
            .Decorate<IProfileRepoWriter>(static (inner, recheck) => new RecheckingProfileRepoWriter(inner, recheck))
            .Decorate<IProfileSandboxWriter>(static (inner, recheck) => new RecheckingProfileSandboxWriter(inner, recheck));
    }

    private static IServiceCollection Decorate<TPort>(this IServiceCollection services, Func<TPort, ProfileRecheck, TPort> wrap)
        where TPort : class
    {
        var registered = services.LastOrDefault(d => d.ServiceType == typeof(TPort) && d.ServiceKey is null);
        if (registered is null)
        {
            return services;
        }

        _ = services.Remove(registered);
        object Inner(IServiceProvider sp) =>
            registered.ImplementationFactory?.Invoke(sp) ?? registered.ImplementationInstance ?? ActivatorUtilities.CreateInstance(sp, registered.ImplementationType ?? throw new InvalidOperationException($"{typeof(TPort).Name} is registered without an implementation"));

        services.Add(new ServiceDescriptor(typeof(TPort), sp => wrap((TPort)Inner(sp), sp.GetRequiredService<ProfileRecheck>()), registered.Lifetime));
        return services;
    }
}
