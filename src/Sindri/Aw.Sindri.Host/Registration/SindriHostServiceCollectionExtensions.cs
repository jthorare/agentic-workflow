using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Host.Onyx;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Host.Shipped;

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
        .AddSingleton<IShippedEffects, EmbeddedShippedEffects>()   // the default profile allows the effects the embedded recipes declare (setup-once §5.1)
        .AddSingleton<IEmbeddedRecipes>(static _ => new ManifestEmbeddedRecipes(typeof(ManifestEmbeddedRecipes).Assembly))   // the recipes this build ships; nothing at run time reads recipes/sindri from disk
        .AddSingleton<Projector>()
        .AddSingleton<ScheduleInputs>()
        .AddSingleton<CodeIndex.Core.Facade.ICodeIndex, CodeIndexHost.UnbuiltCodeIndex>()   // no index store yet: every read answers 'no index' until CI-9 registers the real facade
        .AddScoped<CodeIndexHost.ICodeIndexScopeSource, CodeIndexHost.ProfileCodeIndexScopeSource>()
        .AddScoped<Context.Core.Symbols.ICodeSymbolSource, CodeIndexHost.CodeIndexSymbolSource>()   // Context reads the index through the facade above (S-9); replaces the no-index default (TryAdd in AddContextFeatures)
        .AddSingleton<IOnyxProgress>(static sp => new JobOnyxProgress(new ConsoleOnyxProgress(Console.Error), sp.GetRequiredService<IJobContextAccessor>()));   // `onyx up`'s lines and dots on standard error, or the Onyx setup job's own lines

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
        .AddHostedService<ContextWarmHost>()
        .AddHostedService<LauncherHost>()
        .AddHostedService<LeaseRenewer>();
}
