using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Launch;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Registers the session collector's adapters (collector spec CI-3): the catalog, the /proc table (ps and lsof on macOS), Claude's records, the hook's state files, transcripts and the snapshot.</summary>
internal static class SessionsServiceCollectionExtensions
{
    /// <summary>Adds the adapters; the roots come from launcher.json and the AW_SESSIONS_* and AW_FLEET_DIR variables.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="settings">Sindri's settings.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSindriSessions(this IServiceCollection services, Func<string, string?> environment, SindriSettings settings)
    {
        var load = LauncherStartup.Load(environment, settings);
        var providers = load.Sessions.Providers ?? [.. load.Catalog.All.Select(static a => a.Id)];
        var procOverride = LauncherStartup.NonEmpty(environment("AW_SESSIONS_PROC_ROOT"));
        var procRoot = procOverride ?? "/proc";
        var mac = SessionSourceChoice.UsesMac(procOverride is not null, Directory.Exists(procRoot), OperatingSystem.IsMacOS(), File.Exists(MacProcessTable.Ps));
        var fleet = LauncherStartup.NonEmpty(environment("AW_FLEET_DIR")) ?? Path.Combine(settings.StateDirectory, "fleet");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return services
            .AddSingleton<IAgentCatalog>(load.Catalog)
            .AddSingleton(LauncherStartup.Catalog(environment, settings))
            .AddSingleton(load.Sessions)
            .AddSingleton(mac ? MacSessionSources.Table() : new ProcProcessTable(procRoot, OwnUid.Read()) as IProcessTable)
            .AddSingleton<IClaudeSessionRecords>(new ClaudeSessionRecords(load.ClaudeSessionsDirectory))
            .AddSingleton<IFleetStates>(new FleetStateFiles(fleet, providers, pruning: !load.Sessions.TestOverride))
            .AddSingleton(mac ? MacSessionSources.Sockets() : new UnixSockets(procRoot) as IUnixSockets)
            .AddSingleton(provider => new TmuxCli(provider.GetRequiredService<TimeProvider>(), "tmux", TmuxCli.CallBudget))   // one instance: the pass and the preview share its caches and marks
            .AddSingleton<ITmuxServers>(static provider => provider.GetRequiredService<TmuxCli>())
            .AddSingleton<IPaneReader>(static provider => provider.GetRequiredService<TmuxCli>())
            .AddSingleton<ITeamNames>(new TeamNameFiles(settings.StateDirectory))
            .AddSingleton<ITerminalNames>(TerminalProcessNames.Embedded())
            .AddSingleton<ITranscripts>(new TranscriptFiles(home))
            .AddSingleton<ISessionSnapshot, SessionSnapshotStore>();
    }
}
