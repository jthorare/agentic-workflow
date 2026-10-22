using Aw.Redaction;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Registers the launcher and its parts (API P10a, U7): the tmux host, the configured viewer, the launcher and the box-local setup.</summary>
internal static class LauncherServiceCollectionExtensions
{
    internal static IServiceCollection AddSindriLauncher(this IServiceCollection services, Func<string, string?> environment, SindriSettings settings)
    {
        var builtinHashes = LauncherConfigFile.Read(LauncherStartup.Builtins(), user: null).Value.Agents.ToDictionary(static a => a.Id.Value, static a => a.ArgvHash, StringComparer.Ordinal);
        string? BuiltinHash(AgentId id) => builtinHashes.GetValueOrDefault(id.Value);
        return services
            .AddSingleton(static _ => new TmuxSessionHost(new TmuxHostOptions()))
            .AddSingleton<ISessionHost>(static sp => sp.GetRequiredService<TmuxSessionHost>())   // the launcher service polls it for attached clients (API P10a U9)
            .AddSingleton<NoSessionHost>()
            .AddSingleton<ILaunchStartup>(sp => new LaunchStartup(() => WarpSweep(sp, environment)))
            .AddSingleton<ITerminalViewer>(sp => Viewer(sp, environment))
            .AddSingleton<IReadOnlyDictionary<string, IUsageReader>>(UsageReaders(environment))
            .AddSingleton(sp => new UsageSpend(sp.GetRequiredService<ReloadingAgentCatalog>(), sp.GetRequiredService<IReadOnlyDictionary<string, IUsageReader>>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<ILaunchSpend>(static sp => sp.GetRequiredService<UsageSpend>())
            .AddSingleton<ILaunchUsage>(static sp => sp.GetRequiredService<UsageSpend>())
            .AddScoped<ILaunchProbes, LaunchProbeRecorder>()
            .AddScoped<LaunchStopEnd>()
            .AddSingleton(_ => Options(environment, settings))
            .AddSingleton<ILaunchFiles>(static sp => new LaunchFileStore(sp.GetRequiredService<LauncherOptions>()))
            .AddSingleton<IAgentTrust>(new ClaudeCodeTrust(ClaudeCodeTrust.PathFor(environment)))
            .AddSingleton(sp => new Launcher(Parts(sp, BuiltinHash)))
            .AddSingleton<ILauncher>(static sp => sp.GetRequiredService<Launcher>())
            .AddSingleton<ILaunchShutdown>(static sp => sp.GetRequiredService<Launcher>())
            .AddSingleton<ILaunchSetup>(sp => new LauncherInit(new LauncherInitParts(sp.GetRequiredService<ReloadingAgentCatalog>(), Detection(sp, environment))
            {
                BuiltinHash = BuiltinHash,
                Approved = Approved(sp),
            }));
    }

    private static LauncherOptions Options(Func<string, string?> environment, SindriSettings settings) => new(settings.Home)
    {
        SindriExecutable = SindriExecutable(environment),
        SindriHome = LauncherStartup.NonEmpty(environment("AW_SINDRI_HOME")),
    };

    private static LauncherParts Parts(IServiceProvider sp, Func<AgentId, string?> builtinHash)
    {
        var catalog = sp.GetRequiredService<ReloadingAgentCatalog>();
        var tmux = sp.GetRequiredService<TmuxSessionHost>();
        var none = sp.GetRequiredService<NoSessionHost>();
        return new LauncherParts(catalog, attended => attended && string.Equals(catalog.Current.Host?.Kind, "none", StringComparison.Ordinal) ? none : tmux, sp.GetRequiredService<ITerminalViewer>(), sp.GetRequiredService<LauncherOptions>(), sp.GetRequiredService<TimeProvider>())
        {
            BuiltinHash = builtinHash,
            Approved = Approved(sp),
            McpFor = McpTargetFor(sp),
            Config = () => catalog.Current,
            Trust = sp.GetRequiredService<IAgentTrust>(),
            EndLive = async (launch, cancellationToken) =>
            {
                var scope = sp.CreateAsyncScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<LaunchStopEnd>().EndIfLive(launch, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
            },
            Usage = name => sp.GetRequiredService<IReadOnlyDictionary<string, IUsageReader>>().GetValueOrDefault(name),
        };
    }

    /// <summary>The repo the approved profile names for a checkout path (the launcher passes the profile checkout as RepoPath; an engine step's worktree travels separately in LaunchRequest.Worktree, so only an exact checkout path matches) and the port Sindri answers on: the running instance's, else the profile's.</summary>
    private static Func<string, CancellationToken, ValueTask<McpTarget?>> McpTargetFor(IServiceProvider sp) => async (path, ct) =>
    {
        var latest = await sp.GetRequiredService<IApprovedProfile>().Latest(ct).ConfigureAwait(false);
        var repo = latest.IsError ? null : latest.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Path.TrimEnd('/'), path.TrimEnd('/'), StringComparison.Ordinal));
        if (repo is null)
        {
            return null;
        }

        var running = await sp.GetRequiredService<IEndpointFile>().Read(ct).ConfigureAwait(false);
        return new McpTarget(repo.Name, McpRegistration.Port(running.IsSuccess ? running.Value?.Url : null, latest.Value?.Profile.Api?.Port));
    };

    private static Func<CancellationToken, ValueTask<AgentSettings?>> Approved(IServiceProvider sp) => async ct =>
    {
        var latest = await sp.GetRequiredService<IApprovedProfile>().Latest(ct).ConfigureAwait(false);
        return latest.IsError ? null : latest.Value?.Profile.Agents;
    };

    private static DetectionInputs Detection(IServiceProvider sp, Func<string, string?> environment) =>
        new(environment, ViewerProcess.OnLoginPath, async ct => (await Create(sp, environment, new ViewerSection("warp")).Probe(ct).ConfigureAwait(false)).Present, TerminalTemplates.Load());

    private static ConfiguredViewer Viewer(IServiceProvider sp, Func<string, string?> environment) =>
        new(sp.GetRequiredService<ReloadingAgentCatalog>(), section => Create(sp, environment, section), Detection(sp, environment));

    private static ITerminalViewer Create(IServiceProvider sp, Func<string, string?> environment, ViewerSection section)
    {
        var host = sp.GetRequiredService<TmuxSessionHost>();
        var time = sp.GetRequiredService<TimeProvider>();
        return section.Kind switch
        {
            "warp" when !OperatingSystem.IsWindows() => new WarpViewer(WarpOptions(environment), host, time, Redactor.Default),
            "exec-template" => new ExecTemplateViewer(new ExecTemplateOptions(section.Name!, section.Argv!), host, time, Redactor.Default),   // why: a valid section has both
            _ => new PrintViewer(),
        };
    }

    /// <summary>A restart inside the 60 s window must not leave a launch file in Warp's palette (UI spec 6.3): swept once when the launcher service starts, not on each viewer build.</summary>
    private static void WarpSweep(IServiceProvider sp, Func<string, string?> environment)
    {
        if (!OperatingSystem.IsWindows())
        {
            new WarpViewer(WarpOptions(environment), sp.GetRequiredService<TmuxSessionHost>(), sp.GetRequiredService<TimeProvider>(), Redactor.Default).Sweep();
        }
    }

    /// <summary>The program that runs <c lang="text">session wrap</c> inside the session: <c lang="text">AW_SINDRI_EXE</c>, else this process, else <c lang="text">sindri</c> on the PATH (under <c lang="text">dotnet run</c> the process is the dotnet host, which can't run the wrap).</summary>
    private static string SindriExecutable(Func<string, string?> environment)
    {
        if (LauncherStartup.NonEmpty(environment("AW_SINDRI_EXE")) is { } named)
        {
            return named;
        }

        var self = Environment.ProcessPath;
        return self is null || string.Equals(Path.GetFileNameWithoutExtension(self), "dotnet", StringComparison.OrdinalIgnoreCase) ? "sindri" : self;
    }

    /// <summary>The usage readers over the CLIs' own transcript directories: where the CLIs write by default, or where their own variables point.</summary>
    private static Dictionary<string, IUsageReader> UsageReaders(Func<string, string?> environment)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var claude = LauncherStartup.NonEmpty(environment("CLAUDE_CONFIG_DIR")) ?? Path.Combine(home, ".claude");
        var codex = LauncherStartup.NonEmpty(environment("CODEX_HOME")) ?? Path.Combine(home, ".codex");
        IUsageReader[] readers = [new ClaudeCodeUsage(Path.Combine(claude, "projects")), new CodexUsage(Path.Combine(codex, "sessions")), new CursorUsage(Path.Combine(home, ".cursor", "projects"))];
        return readers.ToDictionary(static r => r.Name, StringComparer.Ordinal);
    }

    private static WarpViewerOptions WarpOptions(Func<string, string?> environment)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var mac = OperatingSystem.IsMacOS();
        var data = LauncherStartup.NonEmpty(environment("XDG_DATA_HOME")) ?? Path.Combine(home, ".local", "share");
        var dir = LauncherStartup.NonEmpty(environment("AW_WARP_LAUNCH_DIR")) ?? (mac ? Path.Combine(home, ".warp", "launch_configurations") : Path.Combine(data, "warp-terminal", "launch_configurations"));
        return new WarpViewerOptions(dir) { IsMac = mac, Opener = mac ? "open" : "xdg-open", GetEnv = environment };
    }
}
