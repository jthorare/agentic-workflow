using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Features.GetOpenRuns;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Features.SkipSetupStep;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the first-run slices (first-run spec §7, A1, A4, A8): the skip, the restart and its open-set read, and the account connect.</summary>
internal static class FirstRunServiceCollectionExtensions
{
    /// <summary>Adds SkipSetupStep, RestartProcess, ConnectAccount and GetOpenRuns behind their chains.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddFirstRun(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<SkipSetupStepRequest>, AcceptAllValidator<SkipSetupStepRequest>>().AddCommand<SkipSetupStepRequest, SkipSetupStepResponse, SkipSetupStepService>()
            .AddScoped<IValidator<RestartProcessRequest>, AcceptAllValidator<RestartProcessRequest>>().AddCommand<RestartProcessRequest, RestartProcessResponse, RestartProcessService>()
            .AddScoped<IValidator<ConnectAccountRequest>, AcceptAllValidator<ConnectAccountRequest>>().AddCommand<ConnectAccountRequest, ConnectAccountResponse, ConnectAccountService>()
            .AddScoped<IValidator<GetOpenRunsRequest>, AcceptAllValidator<GetOpenRunsRequest>>().AddQuery<GetOpenRunsRequest, GetOpenRunsResponse, GetOpenRunsService>();
    }
}
