using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus when a Resume answered the parked Steps (<see cref="ShellWindow"/>, continued; sandbox spec 8).</summary>
internal sealed partial class ShellWindow
{
    // The park card is answered and goes: after the live region spoke "Ready. Resuming 2 Steps", focus moves to its neighbour, as for any control that hid under focus.
    private void ListenSandbox(ShellViewModel shell, bool on)
    {
        if (on)
        {
            shell.Health.Sandbox.Check.Resolved += OnSandboxResolved;
        }
        else
        {
            shell.Health.Sandbox.Check.Resolved -= OnSandboxResolved;
        }
    }

    private void OnSandboxResolved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
}
