using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Infrastructure.Browser;
using Aw.Evidence.Infrastructure.Databases;
using Aw.Evidence.Infrastructure.Desktop;
using Aw.Evidence.Infrastructure.Files;
using Aw.Evidence.Infrastructure.Guard;
using Aw.Evidence.Infrastructure.Http;
using Aw.Evidence.Infrastructure.Ids;
using Aw.Evidence.Infrastructure.Images;
using Aw.Evidence.Infrastructure.Judging;
using Aw.Evidence.Infrastructure.Locks;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Evidence.Infrastructure.Publishing;
using Aw.Evidence.Infrastructure.Redaction;
using Aw.Evidence.Infrastructure.Settings;
using Aw.Evidence.Infrastructure.Stack;
using Aw.Evidence.Infrastructure.Targets;
using Aw.Evidence.Infrastructure.Visual;
using Aw.Kernel.Ids;
using Aw.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Infrastructure;

/// <summary>Registers Aw.Evidence.Infrastructure's adapters and settings. A second technology for a port gets its own AddEvidence&lt;Tech&gt;Infrastructure method.</summary>
public static class EvidenceInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Evidence adapters and their settings: directory locks, run files on disk, the PNG codec, the verdict-cache
    /// manifest, the stack probes (HTTP, docker, TCP), the psql provenance query, environment secrets and settings, the Playwright
    /// browser and its installer, the judge CLI adapter (a stub until it is wired, answering unavailable), the gh runner (a stub until it is wired), and random ids. The verdict-cache store reads the package's AwJson options,
    /// which Composition registers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidenceInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        var settings = SettingsFromEnvironment.Read(environment);
        var database = DatabaseSettings.Read(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton<IMachineLocks>(static sp => new DirectoryLocks(sp.GetRequiredService<EvidenceSettings>().Locks, sp.GetRequiredService<TimeProvider>(), Console.Error))
            .AddSingleton<IRunFiles, FileRunFiles>()
            .AddSingleton<IImageCodec, PngCodec>()
            .AddSingleton<IVerdictCacheStore, FileVerdictCacheStore>()
            .AddSingleton<IStackProbe>(_ => new ProcessStackProbe("docker", database))
            .AddSingleton<IProvenanceQuery>(new DockerPsqlProvenance("docker", database))
            .AddSingleton<ISecretsSource>(new EnvironmentSecrets(environment))
            .AddSingleton<IEvidenceSettings>(new EnvironmentEvidenceSettings(environment))
            .AddSingleton<IBrowser, PlaywrightBrowser>()
            .AddSingleton<IBrowserInstaller, PlaywrightInstaller>()
            .AddSingleton<IJudge, JudgeCliJudge>()
            .AddSingleton<IApiTransport>(static _ => new HttpApiTransport(TimeSpan.FromSeconds(30)))
            .AddSingleton<ICliRunner>(new ProcessCliRunner(TimeSpan.FromMinutes(2)))
            .AddSingleton<IGhRunner, ProcessGhRunner>()
            .AddSingleton<IDesktopCommands, ProcessDesktopCommands>()
            .AddSingleton<IDesktopApps, ProcessDesktopApps>()
            .AddSingleton<IScratchDirectories, TempScratchDirectories>()
            .AddSingleton<IStaticSite, LoopbackStaticSite>()
            .AddSingleton<IExecutableLocator, FileExecutableLocator>()
            .AddSingleton<IEvidenceTargets, FileEvidenceTargets>()
            .AddSingleton<SqliteDatabaseReader>()
            .AddSingleton<ITextScrubber>(new RedactionScrubber(Redactor.Default))
            .AddSingleton<IIdGenerator, RandomIds>();
    }

    /// <summary>
    /// Adds the Postgres database reader (Npgsql) and the router that picks the SQLite or Postgres reader by target. A second technology for
    /// a port gets its own method; <see cref="AddEvidenceInfrastructure"/> must have run first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidencePostgresInfrastructure(this IServiceCollection services) => services
        .AddSingleton<PostgresDatabaseReader>()
        .AddSingleton<IDatabaseReader, DatabaseReaders>();
}
