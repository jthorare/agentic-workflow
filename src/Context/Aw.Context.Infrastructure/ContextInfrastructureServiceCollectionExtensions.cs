using Aw.Context.Core.Code;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.History;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Intent;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Infrastructure.Review;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Sources;
using Aw.Context.Infrastructure.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure;

/// <summary>Registers Aw.Context.Infrastructure's adapters and settings. A second technology for a port gets its own AddContext&lt;Tech&gt;Infrastructure method.</summary>
public static class ContextInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the context store (context.db under $AW_SINDRI_HOME/context), git, the repo-docs index and source catalog,
    /// the scrubber factory over Aw.Redaction (each call's scrubbers carry the profile's extra patterns), the graph store,
    /// the map file reader, the exports folder, the probe file reader and the probe reports folder, and the factory that builds the onyx source from the
    /// profile's onyx settings and Linear teams (doctor's 'context:onyx' check runs that source's probe). The onyx source reads its key by calling
    /// <paramref name="environment"/> with the settings' pointer text (Sindri's Composition wraps the environment so
    /// pointers resolve there). Needs a TimeProvider, ILogger and the host's AwJson options (a JsonSerializerOptions
    /// singleton whose context holds the map types), all registered by the host.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<ContextDatabase>()
            .AddSingleton<GitCli>()
            .AddSingleton<IScrubberFactory, RedactionScrubberFactory>()
            .AddSingleton<ISourceCatalog, ContextSourceCatalog>()
            .AddSingleton<IDocIndex, SqliteDocIndex>()
            .AddSingleton<SqliteGraphStore>()
            .AddSingleton<IGraphStore>(sp => sp.GetRequiredService<SqliteGraphStore>())
            .AddSingleton<IProposalRuns>(sp => sp.GetRequiredService<SqliteGraphStore>())
            .AddSingleton<IIntentStore, SqliteIntentStore>()
            .AddSingleton<IMapFileReader, MapFileReader>()
            .AddSingleton<IExportStore, FileExportStore>()
            .AddSingleton<IReviewFileStore, FileReviewFileStore>()
            .AddSingleton<ICurrentUser, OsCurrentUser>()
            .AddSingleton<IRepoReader, GitRepoReader>()
            .AddSingleton<IDefaultBranch, GitDefaultBranch>()
            .AddSingleton<ICodeSearch, GitCodeSearch>()
            .AddSingleton(GraphifyOptions.Default)
            .AddSingleton<IClusterSource, GraphifyClusterSource>()
            .AddSingleton<IProbeFileReader, ProbeFileReader>()
            .AddSingleton<IProbeReportStore, FileProbeReportStore>()
            .AddSingleton<IMergeHistory, GitMergeHistory>()
            .AddSingleton<IFileHistory, GitFileHistory>()
            .AddSingleton<IBriefSource, OnyxBriefSource>()
            .AddSingleton<IAsOfDocs, AsOfDocsFactory>()
            .AddSingleton<IEvalStore, FileEvalStore>()
            .AddSingleton(sp => new OnyxSources(new OnyxSourceParts(
                OnyxHttp.Client, environment, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<OnyxClient>>())));

    /// <summary>
    /// Adds the labeling pass ('context.labeling'): <see cref="ILabeler"/> over the host's <see cref="Core.Models.IModelPort"/>. The token cap
    /// ('context.labeling.maxTokens') arrives with each pass; a build asks the labeler only when the approved profile turns labeling on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextLabeling(this IServiceCollection services)
        => services.AddSingleton<ILabeler>(sp => new ModelLabeler(
            sp.GetRequiredService<Core.Models.IModelPort>(), sp.GetRequiredService<IScrubberFactory>()));
}
