using System.Runtime.InteropServices;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// Keeps 'session wrap' alive when its terminal signals the process group (Ctrl-C, a closed tab, a killed tmux session): the agent gets the same signal and
/// ends by itself, and wrap must still be there to report its exit. Without this a closed tab would leave the session to the 120 s silence timer.
/// </summary>
internal sealed class WrapSignals : IDisposable
{
    private readonly List<PosixSignalRegistration> _held = [];

    private WrapSignals()
    {
    }

    /// <summary>Starts ignoring the interrupt, terminate and hang-up signals for this process.</summary>
    /// <returns>The hold; disposing it restores the defaults.</returns>
    internal static WrapSignals Hold()
    {
        var signals = new WrapSignals();
        if (!OperatingSystem.IsWindows())
        {
            foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP })
            {
                signals._held.Add(PosixSignalRegistration.Create(signal, static context => context.Cancel = true));
            }
        }

        return signals;
    }

    public void Dispose()
    {
        foreach (var registration in _held)
        {
            registration.Dispose();
        }
    }
}
