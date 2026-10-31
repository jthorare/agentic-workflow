using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Host.Commands;

/// <summary>Composes 'sindri session wrap' for the verb table: the real control plane calls, process and files.</summary>
internal static class SessionWrapVerb
{
    /// <summary>Runs the wrap.</summary>
    /// <param name="args">The arguments after 'session wrap'.</param>
    /// <param name="client">The CLI's control plane client.</param>
    /// <param name="services">Sindri's home and the limit watchdog's ports come from here.</param>
    /// <param name="environment">The process environment.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The agent's exit code; 127 when its CLI is missing; 2 for bad arguments.</returns>
    internal static async Task<int> Run(string[] args, ControlPlaneClient client, IServiceProvider services, Func<string, string?> environment, CancellationToken ct)
    {
        using var held = WrapSignals.Hold();
        var command = new SessionWrapCommand(new ControlPlaneWrapCalls(client), new ProcessChildLauncher(), environment, TimeProvider.System, Console.Error, new SessionIdFiles(services.GetRequiredService<ISindriHome>().Path), new WrapExtras(held.Hangup, Ports(services, OperatingSystem.IsMacOS()), services.GetRequiredService<IHeavyLockSweep>(), LocksDir(environment)));
        return await command.Run(args, ct).ConfigureAwait(false);
    }

    /// <summary>The limit watchdog's ports: the systemd ones on Linux, the process-group ones (and the group id file's release) on macOS.</summary>
    /// <param name="services">Sindri's services.</param>
    /// <param name="mac">Whether this is macOS.</param>
    /// <returns>The ports.</returns>
    internal static WatchdogPorts Ports(IServiceProvider services, bool mac)
    {
        var scope = services.GetRequiredService<IScopeControl>();
        var stats = services.GetRequiredService<ICgroupStats>();
        if (!mac)
        {
            return new WatchdogPorts(scope, stats);
        }

        var groups = ProcessGroupFiles.Directory(services.GetRequiredService<LauncherOptions>().LaunchDirectory);
        return new WatchdogPorts(scope, stats, services.GetRequiredService<IMemoryKills>(), unit => ProcessGroupFiles.Remove(groups, unit));
    }

    private static string LocksDir(Func<string, string?> environment) =>
        Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } root ? root : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow"), "locks");
}
