using System.Text.Json;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Config;
using Aw.Judge.Infrastructure.Ids;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Infrastructure.PromptSort;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Infrastructure.Redaction;
using Aw.Judge.Infrastructure.Secrets;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Ids;
using Aw.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Infrastructure;

/// <summary>Registers Aw.Judge.Infrastructure's adapters and settings. A second technology for a port gets its own AddJudge&lt;Tech&gt;Infrastructure method.</summary>
public static class JudgeInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Judge adapters and their settings: the SQLite stores over $AW_STATE_DIR/judge/decisions.sqlite, the config
    /// file $AW_STATE_DIR/judge/config.json (which needs the host's JsonSerializerOptions registered), the input redactor
    /// over Aw.Redaction, the PATH executable locator, the process runner, the secret source (environment, then keychain),
    /// random decision ids and the model providers (AddModelProviders), with the batched Jev call (IBatchClassifier) beside them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJudgeInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        var settings = SettingsFromEnvironment.Read(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(sp => new JudgeDatabase(JudgeDatabase.OptionsFor(settings.StateDirectory), sp.GetRequiredService<TimeProvider>()))
            .AddScoped<IDecisionLog, SqliteDecisionLog>()
            .AddScoped<IBriefStore, SqliteBriefStore>()
            .AddScoped<IPromptSortStore, SqlitePromptSortStore>()
            .AddScoped<ISortSessionStore, FileSortSessionStore>()
            .AddScoped<IJudgeConfigStore>(sp => new FileJudgeConfigStore(settings.StateDirectory, sp.GetRequiredService<JsonSerializerOptions>()))
            .AddSingleton<IInputRedactor>(new AwInputRedactor(Redactor.Default))
            .AddSingleton<IExecutableLocator>(new PathExecutableLocator(environment))
            .AddSingleton<IProcessRunner>(new SystemProcessRunner(environment))
            .AddSingleton<ISecretSource>(sp => new KeychainSecretSource(environment, sp.GetRequiredService<IProcessRunner>()))
            .AddSingleton<IIdGenerator, RandomIdGenerator>()
            .AddModelProviders(environment);
    }

    /// <summary>
    /// The model providers behind one port, IProviderCatalog: Jev (over its own HttpClient and the secret source) and
    /// the claude, codex and cursor-agent CLIs (over the process runner); the catalog picks and orders them per
    /// config.json, AW_PROVIDER and PATH, and closes every chain with the rules provider its caller passes.
    /// </summary>
    private static IServiceCollection AddModelProviders(this IServiceCollection services, Func<string, string?> environment) => services
        .AddSingleton(sp => new JevProvider(new HttpClient(), sp.GetRequiredService<ISecretSource>(), sp.GetRequiredService<IInputRedactor>()))
        .AddSingleton<IBatchClassifier>(sp => new JevBatchClassifier(sp.GetRequiredService<JevProvider>()))
        .AddSingleton<IProviderCatalog>(sp => new ProviderCatalog(
            environment,
            sp.GetRequiredService<IExecutableLocator>(),
            [
                sp.GetRequiredService<JevProvider>(),
                new ClaudeCliProvider(sp.GetRequiredService<IProcessRunner>()),
                new CodexCliProvider(sp.GetRequiredService<IProcessRunner>()),
                new CursorCliProvider(sp.GetRequiredService<IProcessRunner>(), sp.GetRequiredService<IInputRedactor>()),
            ]));
}
