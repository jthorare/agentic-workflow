using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>API P6 unit C: the job runtime (start recorder, event writer, runner, waits and recovery), outside the command gate.</summary>
internal static class JobRuntimeServiceCollectionExtensions
{
    /// <summary>Adds the job runtime's singletons.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddJobRuntime(this IServiceCollection services) =>
        services
            .AddSingleton(static sp => new JobStartRecorder(sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<ILedgerStore>(), sp.GetRequiredService<EventFactory>()))
            .AddSingleton(static sp => new JobEventWriter(sp.GetRequiredService<ILedgerStore>(), sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<EventFactory>(),
                sp.GetRequiredService<ITextScrubber>(), sp.GetRequiredService<ICallerContextAccessor>()))
            .AddSingleton(static sp => new JobRunner(() => { var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope(); return new JobScope(scope.ServiceProvider.GetRequiredService, scope); }, sp.GetRequiredService<IJobContextAccessor>(),
                sp.GetRequiredService<ICallerContextAccessor>(), sp.GetRequiredService<JobEventWriter>(), sp.GetRequiredService<JobRegistry>(), sp.GetRequiredService<IShutdown>(), sp.GetRequiredService<JobWaits>()))
            .AddSingleton<JobWaits>().AddSingleton<IJobSignals>(static sp => sp.GetRequiredService<JobWaits>())
            .AddSingleton(static sp => new JobRecovery(sp.GetRequiredService<IJobViews>(), sp.GetRequiredService<ILedgerReader>(), sp.GetRequiredService<IProjectorStatus>(),
                sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<JobEventWriter>(), sp.GetRequiredService<JobRegistry>()))
            .AddSingleton<IJobRunner>(static sp => sp.GetRequiredService<JobRunner>()).AddSingleton<IJobCancellation>(static sp => sp.GetRequiredService<JobRunner>());
}
