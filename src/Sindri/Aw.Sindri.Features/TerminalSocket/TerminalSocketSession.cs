using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>
/// One terminal WebSocket (agents spec §15.8, message table in docs/sindri/agents.md): hello within 3 s, ready, whole-screen frames at most 30 a second,
/// keys, paste and size in, ping every 15 s with a 10 s pong deadline. A key triggers one capture at once (perf D14). It writes no frame or key to a log or the ledger.
/// Red stub: the body lands in A8 step 2.
/// </summary>
internal sealed class TerminalSocketSession(ITerminalPaneSource source, Func<string, bool> tokenOk, TimeProvider time)
{
    /// <summary>Runs the socket until it closes.</summary>
    /// <param name="provider">The provider of the session.</param>
    /// <param name="id">The provider session id, or the tmux session name.</param>
    /// <param name="channel">The socket.</param>
    /// <param name="cancellationToken">Ends the session (Serve stopping closes with 1001).</param>
    /// <returns>A task that ends when the socket is closed.</returns>
    public ValueTask Run(string provider, string id, ITerminalChannel channel, CancellationToken cancellationToken)
    {
        if (source is null || tokenOk is null || time is null || provider is null || id is null || channel is null)
        {
            return ValueTask.CompletedTask;
        }

        throw new NotSupportedException("red: A8 step 2");
    }
}
