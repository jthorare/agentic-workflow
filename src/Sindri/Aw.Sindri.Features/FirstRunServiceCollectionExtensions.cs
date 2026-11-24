using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Features.GetOpenRuns;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Features.ScanRepos;
using Aw.Sindri.Features.SkipSetupStep;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features;

/// <summary>Registers the first-run slices (first-run spec §7, A1, A4, A8): the skip, the restart and its open-set read, and the account connect, and the repo scan.</summary>
internal static class FirstRunServiceCollectionExtensions
{
    /// <summary>Adds SkipSetupStep, RestartProcess, ConnectAccount, GetOpenRuns and ScanRepos behind their chains, and the open-set read and the stop port the restart takes.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddFirstRun(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<SkipSetupStepRequest>, AcceptAllValidator<SkipSetupStepRequest>>().AddCommand<SkipSetupStepRequest, SkipSetupStepResponse, SkipSetupStepService>()
            .AddScoped<IValidator<RestartProcessRequest>, AcceptAllValidator<RestartProcessRequest>>().AddRestartCommand<RestartProcessRequest, RestartProcessResponse, RestartProcessService>()
            .AddScoped<AccountProfileWrites>().AddScoped<DeviceAccountStore>().AddSingleton(DeviceSignIn)
            .AddScoped<IValidator<DisconnectAccountRequest>, AcceptAllValidator<DisconnectAccountRequest>>().AddCommand<DisconnectAccountRequest, ConnectAccountResponse, DisconnectAccountService>()
            .AddScoped<IValidator<ConnectAccountRequest>, AcceptAllValidator<ConnectAccountRequest>>().AddCommand<ConnectAccountRequest, ConnectAccountResponse, ConnectAccountService>()
            .AddScoped<IOpenRuns, OpenRunsRead>().AddScoped<IServeStop, ServeStopPort>()
            .AddScoped<IValidator<GetOpenRunsRequest>, AcceptAllValidator<GetOpenRunsRequest>>().AddQuery<GetOpenRunsRequest, GetOpenRunsResponse, GetOpenRunsService>()
            .AddScoped<IValidator<ScanReposRequest>, AcceptAllValidator<ScanReposRequest>>().AddQuery<ScanReposRequest, ScanReposResponse, ScanReposService>();
    }

    // The device sign-in is a singleton; each yes it stores runs DeviceAccountStore in a scope of its own.
    private static DeviceAccountSignIn DeviceSignIn(IServiceProvider sp) => new(
        sp.GetRequiredService<IDeviceSignIn>(), sp.GetRequiredService<IConnectionPresetCatalog>(), sp.GetRequiredService<IProfileSource>(),
        async (tokens, client, actor, ct) =>
        {
            var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                return await scope.ServiceProvider.GetRequiredService<DeviceAccountStore>().Save(tokens, client, actor, ct).ConfigureAwait(false);
            }
        },
        sp.GetService<ILogger<DeviceAccountSignIn>>());
}
