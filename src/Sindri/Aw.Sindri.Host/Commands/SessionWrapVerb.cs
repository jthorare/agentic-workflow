using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host.Commands.SessionWrap;

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
        var command = new SessionWrapCommand(new ControlPlaneWrapCalls(client), new ProcessChildLauncher(), environment, TimeProvider.System, Console.Error, new SessionIdFiles(services.GetRequiredService<ISindriHome>().Path), new WrapExtras(held.Hangup, new WatchdogPorts(services.GetRequiredService<IScopeControl>(), services.GetRequiredService<ICgroupStats>()), services.GetRequiredService<IHeavyLockSweep>(), LocksDir(environment)));
        return await command.Run(args, ct).ConfigureAwait(false);
    }

    private static string LocksDir(Func<string, string?> environment) =>
        Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } root ? root : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow"), "locks");
}
