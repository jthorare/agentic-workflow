using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window's Escape key (<see cref="ShellWindow"/>, continued): what Esc closes, in order.</summary>
internal sealed partial class ShellWindow
{
    // Esc closes the sheet when it shows, or Settings over the Not running card (focus then goes from its hidden control
    // to the page region); otherwise it does nothing here. A sheet hidden while Sindri isn't running stays open, so the
    // keypress isn't spent on what the user can't see and the sheet is back when Sindri is.
    private bool Escape()
    {
        if (Shell is not { } shell)
        {
            return false;
        }

        if (shell.Palette.IsOpen)
        {
            shell.Palette.Close();
            return true;
        }

        if (shell.Shortcuts.IsOpen)
        {
            shell.Shortcuts.Close();
            return true;
        }

        if (EscapeItemLayer(shell))
        {
            return true;
        }

        if (EscapeRunPage(shell))
        {
            return true;
        }

        if (EscapeSheetOrInbox(shell))
        {
            return true;
        }

        if (!shell.LeaveSettings())
        {
            return false;
        }

        Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        return true;
    }

    // Esc on the run page clears the selection, then leaves the run for the Workflows list.
    private static bool EscapeRunPage(ShellViewModel shell)
    {
        if (!shell.IsRunPageShown || shell.IsSheetOpen)
        {
            return false;
        }

        if (!shell.RunPage.ClearSelection())
        {
            shell.CloseRunPage();
        }

        return true;
    }
}
