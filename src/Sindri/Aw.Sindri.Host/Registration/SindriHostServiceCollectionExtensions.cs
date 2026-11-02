using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Host.ContextVectors;
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
        .AddSingleton<Endpoints.IProjectFencing, Mcp.ProjectFencing>()   // a session's Projects answer is fenced like sindri_projects' (the fence is in Mcp; Endpoints cannot name it)
        .AddSingleton<ScheduleInputs>()
        .AddSingleton<Context.Core.Sync.IExternalRecordFeeds>(static sp => new IndexSources.GitHubRecordFeeds(sp.GetRequiredService<Core.Secrets.ISecretPointers>(), sp.GetRequiredService<Connectors.Core.GitHub.IGitHubConnectorFactory>(), IndexSources.GitHubReaders.Create))   // the GitHub feed `index sync` picks up through the record-feed catalog
        .AddSingleton<Context.Core.Sync.IExternalRecordFeeds>(static sp => new IndexSources.SlackRecordFeeds(sp.GetRequiredService<Core.Secrets.ISecretPointers>(), sp.GetRequiredService<Connectors.Core.Slack.ISlackConnectorFactory>(), IndexSources.SlackReaders.Create))   // the Slack feed, present only when the profile lists channels, a workspace and a token that resolves
        .AddContextVectors()   // Context's query embedder and vector scan over Aw.Embeddings (owned-index spec §9.2)
        .AddScoped<CodeIndexHost.ICodeIndexScopeSource, CodeIndexHost.ProfileCodeIndexScopeSource>()
        .AddScoped<Context.Core.Symbols.ICodeSymbolSource, CodeIndexHost.CodeIndexSymbolSource>()   // Context reads the index through the CodeIndex facade (S-9); replaces the no-index default (TryAdd in AddContextFeatures)
        .AddScoped<Context.Core.Glossary.ISymbolVectors, CodeIndexHost.CodeIndexSymbolVectors>();   // Context reads the stored symbol embeddings through the CodeIndex facade (the concept glossary); replaces the no-vectors default (TryAdd in AddContextFeatures)

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
