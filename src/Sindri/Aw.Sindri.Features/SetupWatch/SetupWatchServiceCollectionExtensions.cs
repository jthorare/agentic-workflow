using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Setup;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.SetupWatch;

internal static class SetupWatchServiceCollectionExtensions
{
    /// <summary>Registers the 'setup' built-in service and the queue of its automatic jobs (setup-once spec §5).</summary>
    /// <param name="services">The collection.</param>
    /// <param name="running">Reads how many jobs the runner has (the composition root's, because a slice may not reach the job runner).</param>
    /// <returns>The collection.</returns>
    internal static IServiceCollection AddSetupWatch(this IServiceCollection services, Func<IServiceProvider, int> running) =>
        services
            .AddScoped<IScheduledService, SetupService>()
            .AddSingleton<McpAutoMemory>().AddScoped<McpAutoGate>().AddScoped<McpAutoRegister>()
            .AddSingleton<SetupStepLog>().AddSingleton<SetupMeasureLog>().AddScoped<SetupMeasures>().AddScoped<SetupStepRecorder>().AddScoped<SetupStepWatch>()
            .AddSingleton(SetupService.BuiltIn)   // runs without a profile entry, so PauseService refuses it
            .AddSingleton(sp => new SetupJobQueue(() => running(sp)))
            .AddSingleton<Core.Setup.ISetupJobSlots>(static sp => sp.GetRequiredService<SetupJobQueue>());
}
