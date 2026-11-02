using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetEngine;
using Aw.Sindri.Features.GetEngineHistory;
using Aw.Sindri.Features.GetLaunchPicks;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListEngineInstances;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features;

/// <summary>Registers the engine in depth's three queries (API P13, UI E-30) and the model picks read (model-pick spec §6.1).</summary>
internal static class EngineServiceCollectionExtensions
{
    internal static IServiceCollection AddEngineReads(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IEngineReader, NoEngineReader>();   // the Temporal adapter (Aw.Sindri.Workflows) replaces it
        services.TryAddSingleton<IMirroredSequences, NoMirroredSequences>();   // the ledger adapter replaces it
        return services
            .AddSingleton<StuckInstanceCount>()   // GET /status's stuck count, kept ten seconds
            .AddScoped<IValidator<GetEngineRequest>, AcceptAllValidator<GetEngineRequest>>().AddQuery<GetEngineRequest, EngineView, GetEngineService>()
            .AddScoped<IValidator<ListEngineInstancesRequest>, AcceptAllValidator<ListEngineInstancesRequest>>().AddQuery<ListEngineInstancesRequest, EngineInstanceList, ListEngineInstancesService>()
            .AddScoped<IValidator<GetEngineHistoryRequest>, AcceptAllValidator<GetEngineHistoryRequest>>().AddQuery<GetEngineHistoryRequest, EngineHistory, GetEngineHistoryService>()
            .AddScoped<IValidator<GetLaunchPicksRequest>, AcceptAllValidator<GetLaunchPicksRequest>>().AddQuery<GetLaunchPicksRequest, LaunchPicks, GetLaunchPicksService>();   // model-pick spec §6.1
    }
}
