using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Host.ContextVectors;
using Aw.Sindri.Host.Memory;
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
        .AddSingleton<Endpoints.IProjectFencing, Mcp.ProjectFencing>()   // a session's Projects answer is fenced like sindri_projects' (the fence is in Mcp; Endpoints cannot name it)
        .AddSingleton<Endpoints.IMemoryFencing, Mcp.MemoryFencing>()   // a session memory read is fenced like the memory tools answers
        .AddSingleton<Endpoints.ITodoFencing, Mcp.TodoFencing>()   // a session's todo text, a piped CLI's and the ledger's todo events are fenced (todo spec §4.4)
        .AddSingleton<Endpoints.IReviewFencing, Mcp.ReviewFencing>()   // a session's Reviews list is fenced the same way
        .AddSingleton<Endpoints.ILibraryDocFencing, Mcp.LibraryDocFencing>()   // and a design doc's title, path and error text
        .AddSingleton<Endpoints.ILinearIssueFencing, Mcp.LinearIssueFencing>()   // and the Linear issue list's titles and names
        .AddSingleton<ScheduleInputs>()
        .AddScoped<Features.LinearConnection.ILinearKeyChecks>(static sp => new IndexSources.LinearKeyChecks(sp.GetRequiredService<Core.Profile.IProfileSource>(), sp.GetRequiredService<Context.Core.Facade.IContextEngine>(), IndexSources.LinearTeamReader.Create))   // Settings, Connections, Linear asks Linear about a pasted key
        .AddSingleton<Context.Core.Linear.ILinearReadCredential>(static sp => new IndexSources.LinearReadCredential(sp.GetRequiredService<Core.Connections.IConnectionCredentials>()))   // the Linear feed, viewer and lookups read with the connected Linear connection first (#2508)
        .AddSingleton<Context.Core.Sync.IExternalRecordFeeds>(static sp => new IndexSources.GitHubRecordFeeds(sp.GetRequiredService<Core.Secrets.ISecretPointers>(), sp.GetRequiredService<Connectors.Core.GitHub.IGitHubConnectorFactory>(), sp.GetRequiredService<Core.Connections.IConnectionStore>(), sp.GetRequiredService<Core.Connections.IConnectionSecrets>(), IndexSources.GitHubReaders.Create))   // the GitHub feed `index sync` picks up through the record-feed catalog
        .AddSingleton<Context.Core.Sync.IExternalRecordFeeds>(static sp => new IndexSources.SlackRecordFeeds(sp.GetRequiredService<Core.Connections.IConnectionCredentials>(), sp.GetRequiredService<Connectors.Core.Slack.ISlackConnectorFactory>(), IndexSources.SlackReaders.Create))   // the Slack feed, present only when the profile lists channels, a workspace and a token that resolves
        .AddMemoryEmbedder()   // memory's embedding port over the same pinned model (agent-first PR 13)
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
        .AddHostedService<QueuedClockStart>()   // the queued count starts at serve start (#3480)
        .AddHostedService<SchedulerHost>()
        .AddHostedService<ProjectorHost>()
        .AddHostedService<JobRecoveryHost>()
        .AddHostedService<ToolkitRecoveryHost>()
        .AddHostedService<ToolkitAdoptionHost>()
        .AddHostedService<GraphRunRecoveryHost>()
        .AddHostedService<RunItemRepairHost>()
        .AddHostedService<LostSessionsHost>()
        .AddHostedService<SessionUsageHost>()
        .AddHostedService<ProfileWatch>()
        .AddSingleton<IFolderWatch, FileFolderWatch>().AddSingleton<NoteFolder>().AddSingleton<NotePasses>().AddSingleton<TodoEventTail>()
        .AddHostedService<TodoNoteWatch>()
        .AddHostedService<SessionCollectorHost>()
        .AddHostedService<ContextWarmHost>()
        .AddHostedService<SpendWarmHost>()
        .AddHostedService<LauncherHost>()
        .AddHostedService<LeaseRenewer>();
}
