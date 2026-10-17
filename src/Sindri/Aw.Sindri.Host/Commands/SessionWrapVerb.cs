using Aw.Sindri.Core.Launch;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Host.Commands;

/// <summary>Composes 'sindri session wrap' for the verb table: the real control plane calls, process and files.</summary>
internal static class SessionWrapVerb
{
    /// <summary>Runs the wrap.</summary>
    /// <param name="args">The arguments after 'session wrap'.</param>
    /// <param name="client">The CLI's control plane client.</param>
    /// <param name="home">Sindri's home.</param>
    /// <param name="environment">The process environment.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The agent's exit code; 127 when its CLI is missing; 2 for bad arguments.</returns>
    internal static async Task<int> Run(string[] args, ControlPlaneClient client, ISindriHome home, Func<string, string?> environment, CancellationToken ct)
    {
        using var held = WrapSignals.Hold();
        var command = new SessionWrapCommand(new ControlPlaneWrapCalls(client), new ProcessChildLauncher(), environment, TimeProvider.System, Console.Error, new SessionIdFiles(home.Path));
        return await command.Run(args, ct).ConfigureAwait(false);
    }
}
