using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Host.Registration;

/// <summary>Registers the Host's own types: the shutdown port, the exit-code sink, the command catalog, the serve port, and serve's background services.</summary>
internal static class SindriHostServiceCollectionExtensions
{
    /// <summary>Adds the process shutdown (also as IShutdown), the exit code, the command catalog and the projector.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSindriHost(this IServiceCollection services) => services
        .AddSingleton<ProcessShutdown>()
        .AddSingleton<IShutdown>(static sp => sp.GetRequiredService<ProcessShutdown>())
        .AddSingleton<IExitCode, ProcessExitCode>()
        .AddSingleton<ICommandCatalog, CommandCatalog>()
        .AddSingleton<Projector>()
        .AddSingleton<ScheduleInputs>();

    /// <summary>
    /// Adds what only 'sindri serve' runs: the port it chooses before it binds (and the check that refuses to start without
    /// it), the scheduler, the projector loop, the profile watch and the lease renewer.
    /// </summary>
    /// <param name="services">The service collection (already composed).</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSindriServe(this IServiceCollection services) => services
        .AddSingleton<ServePort>()
        .AddHostedService<ServeListenerCheck>()
        .AddHostedService<SchedulerHost>()
        .AddHostedService<ProjectorHost>()
        .AddHostedService<JobRecoveryHost>()
        .AddHostedService<LostSessionsHost>()
        .AddHostedService<SessionUsageHost>()
        .AddHostedService<ProfileWatch>()
        .AddHostedService<SessionCollectorHost>()
        .AddHostedService<LauncherHost>()
        .AddHostedService<LeaseRenewer>();
}
