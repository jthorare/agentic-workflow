using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ConnectOnyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Features.SetupOnyx;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Onyx setup from the app (setup-once spec §5.6): the job that brings the stack up and waits for the owner's login, the command that hands it the login, and the single-use places the login passes through.</summary>
internal static class OnyxSetupServiceCollectionExtensions
{
    /// <summary>Adds the 'SetupOnyx' job, the 'ConnectOnyx' command, and the login's slots and hand-over.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddOnyxSetup(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddSingleton<IOnyxLoginSlots, OnyxLoginSlots>()
            .AddScoped<IOnyxLoginHand, OnyxLoginHand>()
            .AddScoped<IOnyxStackUp, OnyxStackUp>()
            .AddScoped<IOnyxLoginConnector, OnyxLoginConnector>()
            .AddJob<SetupOnyxRequest, SetupOnyxResult, SetupOnyxService>(static _ => OnyxSetupJob.Key, static r => Summary(r))
            .AddScoped<IValidator<ConnectOnyxRequest>, AcceptAllValidator<ConnectOnyxRequest>>().AddCommand<ConnectOnyxRequest, JobStartedResponse, ConnectOnyxService>();
    }

    private static string Summary(SetupOnyxResult result) => result.ProfileChanged ? "Onyx is set up and connected; its section is in your profile" : "Onyx is set up and connected";
}
