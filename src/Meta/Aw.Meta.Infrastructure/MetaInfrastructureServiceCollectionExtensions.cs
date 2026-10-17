using System.Text.Json;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Build;
using Aw.Meta.Infrastructure.Config;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Infrastructure.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Infrastructure;

/// <summary>Registers Aw.Meta.Infrastructure's adapters and settings. A second technology for a port gets its own AddMeta&lt;Tech&gt;Infrastructure method.</summary>
public static class MetaInfrastructureServiceCollectionExtensions
{
    private const string DefaultLogFile = "log.md";

    /// <summary>
    /// Adds the Meta adapters and their settings: the SQLite stores over $AW_STATE_DIR/meta/meta.db, the team config file
    /// (which needs the host's JsonSerializerOptions registered), the transcript reader, the sessions directory, the host's
    /// process table and resources (/proc on Linux, sysctl, vm_stat and ps on macOS), the process command runner and the running log (the config's log path, else log.md in the state directory).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        var settings = SettingsFromEnvironment.Read(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(settings.Paths)
            .AddSingleton(settings.Layout)
            .AddSingleton(sp => new MetaDatabase(MetaDatabase.OptionsFor(settings.StateDirectory), sp.GetRequiredService<TimeProvider>()))
            .AddScoped<IMessageStore, SqliteMessageStore>()
            .AddScoped<ILaneStore, SqliteLaneStore>()
            .AddScoped<IOwnerStore, SqliteOwnerStore>()
            .AddScoped<IGateStore, SqliteGateStore>()
            .AddScoped<IKeyValueStore, SqliteKeyValueStore>()
            .AddScoped<ILaneMail, SqliteLaneMail>()
            .AddScoped<IMetaConfig>(sp => new FileMetaConfig(settings.ConfigPath, new JsonSerializerOptions(sp.GetRequiredService<JsonSerializerOptions>()) { WriteIndented = true }))
            .AddSingleton<ITranscriptReader, FileTranscriptReader>()
            .AddSingleton<ILaneFiles>(new DirectoryLaneFiles(settings.LaneSessionsDirectory))
            .AddScoped<ILanePane>(sp => new TmuxLanePane(sp.GetRequiredService<ICommandRunner>(), settings.Tmux, settings.TmuxSocket, sp.GetRequiredService<IMetaConfig>()))
            .AddScoped<ISessionDirectory>(sp => new LaneSessionDirectory(
                new DirectorySessionDirectory(settings.SessionsDirectory), sp.GetRequiredService<IMetaConfig>(), sp.GetRequiredService<ILaneFiles>(), sp.GetRequiredService<ILanePane>()))
            .AddScoped<ITeamCatalog>(sp => new FileTeamCatalog(settings.Layout, sp.GetRequiredService<ISessionDirectory>(), sp.GetRequiredService<JsonSerializerOptions>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IProcessTable>(sp => OperatingSystem.IsMacOS()
                ? new MacProcessTable(settings.StateDirectory, sp.GetRequiredService<ICommandRunner>())
                : new ProcProcessTable(settings.StateDirectory))
            .AddSingleton<IProcessLocations, ProcessScanner>()
            .AddSingleton<IBuildInfo, AssemblyBuildInfo>()
            .AddSingleton<IHostResources>(sp => OperatingSystem.IsMacOS()
                ? new MacHostResources(settings.StateDirectory, settings.HeavyLock, sp.GetRequiredService<ICommandRunner>())
                : new ProcHostResources(settings.StateDirectory, settings.HeavyLock))
            .AddSingleton<IHeavyReserve>(new FileHeavyReserve(settings.StateDirectory))
            .AddSingleton<ILogFiles, FileLogFiles>()
            .AddSingleton<IWorktreeFiles, DirectoryWorktreeFiles>()
            .AddSingleton<IDelay>(sp => new ClockDelay(sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<ICommandRunner, SystemCommandRunner>()
            .AddScoped<IRunningLog>(sp => new AppendRunningLog(async cancellationToken =>
            {
                var config = await sp.GetRequiredService<IMetaConfig>().Load(cancellationToken).ConfigureAwait(false);
                return config.IsSuccess && config.Value.Log is { Length: > 0 } log ? log : Path.Combine(settings.StateDirectory, DefaultLogFile);
            }));
    }
}
